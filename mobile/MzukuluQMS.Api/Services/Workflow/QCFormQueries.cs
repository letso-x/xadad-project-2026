using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.SignOffs;
namespace MzukuluQMS.Api.Services.Workflow;
public sealed class QCFormQueries(QmsDbSession session, IQCWorkflow workflow, IQCFormContentHashService hash)
{
    public async Task<IReadOnlyList<QCFormDto>> ListAsync(long projectId, int page, int pageSize, string? status, CancellationToken ct)
    {
        if (projectId<=0 || page<1 || page>1000000 || pageSize<1 || pageSize>100) throw new ArgumentException("Valid project, page (1–1000000) and pageSize (1–100) are required.");
        await using var access = await workflow.BeginAsync(null,projectId,"ReadInspection",ct);
        var rows = await session.Connection!.QueryAsync<QCFormDto>(new CommandDefinition("""
            SELECT "QCFormID","ProjectID","ChecklistTemplateID","ChecklistTemplateVersionID","FormNumber",
              "Status"::text AS "Status","StartedByUserID","AssignedToUserID","StartedAt","SubmittedAt","CreatedAt","UpdatedAt","Revision"
            FROM public."QCForm" WHERE "ProjectID"=@projectId AND (@status IS NULL OR "Status"::text=@status)
            ORDER BY "QCFormID" DESC LIMIT @pageSize OFFSET @offset
            """, new { projectId,status,pageSize,offset=(page-1)*pageSize },session.Transaction,cancellationToken:ct));
        return rows.AsList();
    }
    public async Task<IReadOnlyList<SignOffHistoryRow>> SignOffsAsync(long formId, CancellationToken ct)
    {
        await using var access=await workflow.BeginAsync(formId,null,"ReadInspection",ct);
        var currentHash=await hash.GenerateHashAsync(formId,ct);
        return (await session.Connection!.QueryAsync<SignOffHistoryRow>(new CommandDefinition("""
            SELECT s."QCSignOffID",s."UserID",s."SignOffType"::text AS "SignOffType",s."SignatureEvidenceID",s."SignedAt",s."Comments",s."ContentHash",s."HashVersion",
              (s."HashVersion"=2 AND s."ContentHash"=@currentHash AND s."SignedAt">=q."SubmittedAt") AS "MatchesCurrentContentAndSubmission"
            FROM public."QCSignOff" s JOIN public."QCForm" q ON q."QCFormID"=s."QCFormID"
            WHERE s."QCFormID"=@formId ORDER BY s."QCSignOffID"
            """,new { formId,currentHash },session.Transaction,cancellationToken:ct))).AsList();
    }
    public async Task<IReadOnlyList<AuditHistoryRow>> AuditAsync(long formId, long afterId, int limit, CancellationToken ct)
    {
        if(afterId<0 || limit<1 || limit>100) throw new ArgumentException("Invalid audit pagination.");
        await using var access=await workflow.BeginAsync(formId,null,"ReadAudit",ct);
        return (await session.Connection!.QueryAsync<AuditHistoryRow>(new CommandDefinition("""
            SELECT "AuditEventID","ActorUserID","Action","OccurredAt","Details"::text AS "DetailsJson"
            FROM qms_security."AuditEvent" WHERE "EntityType"='QCForm' AND "EntityID"=@formId AND "AuditEventID">@afterId
            ORDER BY "AuditEventID" LIMIT @limit
            """,new { formId,afterId,limit },session.Transaction,cancellationToken:ct))).AsList();
    }
}
public sealed record SignOffHistoryRow
{
 public long QCSignOffID {get;init;} public Guid UserID {get;init;} public string SignOffType {get;init;}="";
 public long SignatureEvidenceID {get;init;} public DateTimeOffset SignedAt {get;init;} public string? Comments {get;init;}
 public string ContentHash {get;init;}=""; public int HashVersion {get;init;}
 // Applicability only, not a claim that signer authority/evidence/approval prerequisites are valid.
 public bool MatchesCurrentContentAndSubmission {get;init;}
}
public sealed record AuditHistoryRow
{
 public long AuditEventID {get;init;} public Guid ActorUserID {get;init;} public string Action {get;init;}="";
 public DateTimeOffset OccurredAt {get;init;} public string DetailsJson {get;init;}="";
}
