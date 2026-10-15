using System.Text.Json;
using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;

public sealed class QCWorkflow(QmsDbSession session, ICurrentUserService users, IHttpContextAccessor context) : IQCWorkflow
{
    public async Task<IQCOperation> BeginAsync(long? formId, long? projectId, string permission, CancellationToken ct)
    {
        if (formId is <= 0 || projectId is <= 0) throw new ArgumentException("Resource ID must be greater than zero.");
        var actor = await users.GetCurrentUserAsync(ct);
        await session.BeginAsync(ct);
        try
        {
            if (formId.HasValue)
                projectId = await session.Connection!.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                    "SELECT \"ProjectID\" FROM public.\"QCForm\" WHERE \"QCFormID\"=@formId FOR UPDATE", new { formId }, session.Transaction, cancellationToken: ct))
                    ?? throw new KeyNotFoundException("QC form was not found.");
            var allowed = await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (
                    SELECT 1 FROM public."User" u
                    JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text AND rp."Permission"=@permission
                    JOIN qms_security."ProjectAccess" pa ON pa."UserID"=u."UserID" AND pa."ProjectID"=@projectId
                    WHERE u."UserID"=@UserID AND u."ExternalAuthID"=@ExternalAuthID AND u."IsActive"=true
                )
                """, new { actor.UserID, actor.ExternalAuthID, permission, projectId }, session.Transaction, cancellationToken: ct));
            if (!allowed) throw new ForbiddenException("This operation is not permitted for this project.");
            Guid? operationKey=context.HttpContext?.Items["QmsOperationKey"] as Guid?;
            if(operationKey.HasValue)
            {
                var fingerprint=(string)context.HttpContext!.Items["QmsOperationFingerprint"]!;
                await session.Connection!.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO qms_security."Operation" ("UserID","OperationID","Fingerprint") VALUES (@user,@operationKey,@fingerprint)
                    ON CONFLICT ("UserID","OperationID") DO NOTHING
                    """,new { user=actor.UserID,operationKey,fingerprint },session.Transaction,cancellationToken:ct));
                var saved=await session.Connection!.QuerySingleAsync<SavedOperation>(new CommandDefinition("""
                    SELECT "Fingerprint","StatusCode","ResultJson","Revision","QCFormID" FROM qms_security."Operation"
                    WHERE "UserID"=@user AND "OperationID"=@operationKey FOR UPDATE
                    """,new { user=actor.UserID,operationKey },session.Transaction,cancellationToken:ct));
                if(saved.Fingerprint!=fingerprint) throw new ConflictException("Idempotency key was already used with different request bytes or resource.");
                if(saved.StatusCode.HasValue) throw new OperationReplayException(saved.StatusCode.Value,saved.ResultJson ?? "null",saved.Revision,saved.QCFormID);
            }
            if (formId.HasValue && context.HttpContext?.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
            {
                var supplied = context.HttpContext.Request.Headers.IfMatch.ToString();
                if (!long.TryParse(supplied.Trim('"'), out var expected) || expected < 0)
                    throw new ArgumentException("If-Match with the current numeric revision is required for form writes.");
                var actual = await session.Connection!.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT \"Revision\" FROM public.\"QCForm\" WHERE \"QCFormID\"=@formId", new { formId },session.Transaction,cancellationToken:ct));
                if (actual != expected) throw new ConflictException("The central record changed. Preserve local work and reload the current revision.");
            }
            return new Operation(session, actor.UserID, operationKey, context);
        }
        catch { await session.DisposeAsync(); throw; }
    }

    public async Task EnsureSigningAuthorityAsync(long formId, string signOffType, CancellationToken ct)
    {
        var actor = await users.GetCurrentUserAsync(ct);
        var allowed = await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1 FROM public."User" u
                JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text
                WHERE u."UserID"=@UserID AND u."IsActive" AND rp."Permission"=@permission
            )
            """, new { actor.UserID, permission = "SignInspection:" + signOffType }, session.Transaction, cancellationToken: ct));
        if (!allowed) throw new ForbiddenException("Signing authority is not configured for this signoff type.");
    }

    public async Task EnsureCurrentSignaturesAsync(long formId, string currentHash, CancellationToken ct)
    {
        if(await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM qms_core."Ncr" WHERE "QCFormID"=@formId AND "ClosedAt" IS NULL AND COALESCE("IsBlocking",true))
            """,new {formId},session.Transaction,cancellationToken:ct)))
            throw new InvalidOperationException("Unresolved blocking or unclassified NCR prevents approval.");
        if(await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM public."NCR" legacy WHERE legacy."QCFormID"=@formId AND legacy."ClosedAt" IS NULL
              AND (NOT EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID")
                OR EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID" AND (n."ClosedAt" IS NOT NULL OR COALESCE(n."IsBlocking",true)))))
            """,new {formId},session.Transaction,cancellationToken:ct)))
            throw new InvalidOperationException("An unresolved historical NCR or inconsistent NCR closure prevents approval; reconcile its blocking policy and verification history.");
        // Requirements are execution snapshots, never the latest live template policy.
        var required = (await session.Connection!.QueryAsync<string>(new CommandDefinition("""
            SELECT "SignOffType" FROM qms_security."FormSignatureRequirement" WHERE "QCFormID"=@formId ORDER BY "SignOffType"
            """, new { formId }, session.Transaction, cancellationToken: ct))).ToList();
        if (required.Count == 0) throw new InvalidOperationException("Required signature policy has not been configured for this form.");
        var submittedAt=await session.Connection!.QuerySingleAsync<DateTime?>(new CommandDefinition(
            "SELECT \"SubmittedAt\" FROM public.\"QCForm\" WHERE \"QCFormID\"=@formId",new {formId},session.Transaction,cancellationToken:ct));
        var history=(await session.Connection!.QueryAsync<SignatureCandidate>(new CommandDefinition("""
            SELECT s."SignOffType"::text AS "SignOffType",s."HashVersion",s."ContentHash",s."SignedAt",
                e."CreatedAt" AS "EvidenceCreatedAt",e."QCFormFieldID",COALESCE(u."IsActive",false) AS "IsActive",
                EXISTS(SELECT 1 FROM qms_security."RolePermission" rp WHERE rp."RoleName"=u."Role"::text AND rp."Permission"='SignInspection')
                    AND EXISTS(SELECT 1 FROM qms_security."RolePermission" rp WHERE rp."RoleName"=u."Role"::text AND rp."Permission"='SignInspection:'||s."SignOffType"::text) AS "HasAuthority",
                EXISTS(SELECT 1 FROM qms_security."ProjectAccess" pa WHERE pa."UserID"=s."UserID" AND pa."ProjectID"=q."ProjectID") AS "HasProjectAccess",
                COALESCE(e."QCFormID"=s."QCFormID" AND e."EvidenceType"::text='Signature' AND e."UploadedByUserID"=s."UserID",false) AS "HasValidEvidence"
            FROM public."QCSignOff" s JOIN public."QCForm" q ON q."QCFormID"=s."QCFormID"
            LEFT JOIN public."Evidence" e ON e."EvidenceID"=s."SignatureEvidenceID"
            LEFT JOIN public."User" u ON u."UserID"=s."UserID"
            WHERE s."QCFormID"=@formId ORDER BY s."QCSignOffID"
            """,new {formId},session.Transaction,cancellationToken:ct))).AsList();
        var requiredFields=(await session.Connection!.QueryAsync<long>(new CommandDefinition("""
            SELECT f."QCFormFieldID" FROM public."QCFormField" f JOIN public."QCFormSection" sec ON sec."QCFormSectionID"=f."QCFormSectionID"
            WHERE sec."QCFormID"=@formId AND (f."RequiresSignature" OR (f."FieldType"::text='Signature' AND f."IsRequired"))
            ORDER BY f."QCFormFieldID"
            """,new {formId},session.Transaction,cancellationToken:ct))).AsList();
        SignatureRules.RequireCurrent(required,requiredFields,history,currentHash,submittedAt);
    }

    private sealed class SavedOperation
    {
        public string Fingerprint {get;init;}=""; public int? StatusCode {get;init;} public string? ResultJson {get;init;}
        public long Revision {get;init;} public long QCFormID {get;init;}
    }
    private sealed class Operation(QmsDbSession session, Guid actor, Guid? operationKey, IHttpContextAccessor context) : IQCOperation
    {
        public async Task CompleteAsync(string action, long formId, object? details, CancellationToken ct, object? result = null)
        {
            await session.Connection!.ExecuteAsync(new CommandDefinition("""
                UPDATE public."QCForm" SET "Revision"="Revision"+1,"UpdatedAt"=clock_timestamp() WHERE "QCFormID"=@formId AND @action<>'QCFormCreated';
                INSERT INTO qms_security."AuditEvent" ("ActorUserID","Action","EntityType","EntityID","Details")
                VALUES (@actor,@action,'QCForm',@formId,CAST(@details AS jsonb))
                """, new { actor, action, formId, details = JsonSerializer.Serialize(details) }, session.Transaction, cancellationToken: ct));
            var revision=await session.Connection!.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT \"Revision\" FROM public.\"QCForm\" WHERE \"QCFormID\"=@formId",new {formId},session.Transaction,cancellationToken:ct));
            if(action is "QCFormReturnedForCorrection" or "NCRCreated")
                await session.Connection!.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO qms_core."Notification" ("UserID","QCFormID","FormRevision","Kind")
                    SELECT DISTINCT u."UserID",q."QCFormID",q."Revision",@action
                    FROM public."QCForm" q JOIN public."User" u ON u."UserID" IN(q."StartedByUserID",q."AssignedToUserID") AND u."IsActive"
                    JOIN qms_security."ProjectAccess" pa ON pa."UserID"=u."UserID" AND pa."ProjectID"=q."ProjectID"
                    JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text AND rp."Permission"='ReadInspection'
                    WHERE q."QCFormID"=@formId ON CONFLICT DO NOTHING
                    """,new {action,formId},session.Transaction,cancellationToken:ct));
            if(operationKey.HasValue)
                await session.Connection!.ExecuteAsync(new CommandDefinition("""
                    UPDATE qms_security."Operation" SET "StatusCode"=@status,"ResultJson"=@json,"Revision"=@revision,"QCFormID"=@formId
                    WHERE "UserID"=@actor AND "OperationID"=@operationKey
                    """,new { status=result is null?204:201,json=JsonSerializer.Serialize(result,new JsonSerializerOptions(JsonSerializerDefaults.Web)),revision,formId,actor,operationKey },session.Transaction,cancellationToken:ct));
            await session.CommitAsync(ct);
            if(context.HttpContext is not null) context.HttpContext.Response.Headers.ETag="\""+revision+"\"";
        }
        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
