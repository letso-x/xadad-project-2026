using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;
public sealed class NcrService(QmsDbSession session,IQCWorkflow workflow,ICurrentUserService users)
{
 public async Task RecordFailureAsync(long formId,long responseId,CancellationToken ct)
 {
  var actor=await users.GetCurrentUserAsync(ct);
  var created=await session.Connection!.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
    INSERT INTO qms_core."Ncr" ("ProjectID","QCFormID","QCResponseID","QCFormFieldID","CreatedByUserID","IsBlocking")
    SELECT q."ProjectID",q."QCFormID",r."QCResponseID",r."QCFormFieldID",@actor,p."DefaultBlocking"
    FROM public."QCResponse" r JOIN public."QCForm" q ON q."QCFormID"=r."QCFormID"
    LEFT JOIN qms_core."NcrPolicy" p ON p."ChecklistTemplateVersionID"=q."ChecklistTemplateVersionID"
    WHERE q."QCFormID"=@formId AND r."QCResponseID"=@responseId
    ON CONFLICT ("QCResponseID") WHERE "ClosedAt" IS NULL DO NOTHING RETURNING "NcrID"
    """,new {formId,responseId,actor=actor.UserID},session.Transaction,cancellationToken:ct));
  if(created.HasValue)await session.Connection!.ExecuteAsync(new CommandDefinition("""
    INSERT INTO qms_security."AuditEvent"("ActorUserID","Action","EntityType","EntityID","Details")
    VALUES(@actor,'NCRCreated','QCForm',@formId,jsonb_build_object('ncrID',@ncrId,'responseID',@responseId))
    """,new {actor=actor.UserID,formId,ncrId=created.Value,responseId},session.Transaction,cancellationToken:ct));
 }
 public async Task<IReadOnlyList<NcrRow>> ListAsync(long formId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(formId,null,"ReadNcr",ct);
  return (await session.Connection!.QueryAsync<NcrRow>(new CommandDefinition("""
    SELECT * FROM qms_core."Ncr" WHERE "QCFormID"=@formId ORDER BY "NcrID" DESC LIMIT 100
    """,new {formId},session.Transaction,cancellationToken:ct))).AsList();
 }
 public async Task CorrectAsync(long formId,long ncrId,CorrectNcrRequest request,CancellationToken ct)
 {
  NcrRules.ValidateCorrection(request);
  await using var operation=await workflow.BeginAsync(formId,null,"CorrectNcr",ct);var actor=await users.GetCurrentUserAsync(ct);
  var count=await session.Connection!.ExecuteAsync(new CommandDefinition("""
    UPDATE qms_core."Ncr" SET "Severity"=@Severity,"Classification"=@Classification,"IsBlocking"=@IsBlocking,
     "Disposition"=@Disposition,"CorrectiveAction"=@CorrectiveAction,"CorrectedByUserID"=@actor,"CorrectiveEvidenceID"=@CorrectiveEvidenceID,
     "VerificationPassed"=NULL,"VerifierUserID"=NULL,"VerifiedAt"=NULL,"VerificationComments"=NULL,"Revision"="Revision"+1
    WHERE "NcrID"=@ncrId AND "QCFormID"=@formId AND "ClosedAt" IS NULL AND "Revision"=@ExpectedRevision
     AND EXISTS(SELECT 1 FROM public."Evidence" e WHERE e."EvidenceID"=@CorrectiveEvidenceID AND e."QCFormID"=@formId AND e."EvidenceType"::text<>'Signature')
    """,new {request.Severity,request.Classification,request.IsBlocking,request.Disposition,request.CorrectiveAction,request.CorrectiveEvidenceID,request.ExpectedRevision,ncrId,formId,actor=actor.UserID},session.Transaction,cancellationToken:ct));
  if(count!=1)throw new InvalidOperationException("NCR changed/closed, or corrective evidence does not belong to this form.");
  await operation.CompleteAsync("NCRCorrected",formId,new {ncrId},ct);
 }
 public async Task VerifyAsync(long formId,long ncrId,VerifyNcrRequest request,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(request.Comments) || request.Comments.Length>2000)throw new ArgumentException("Verification comments are required (max 2000).");
  await using var operation=await workflow.BeginAsync(formId,null,"VerifyNcr",ct);var actor=await users.GetCurrentUserAsync(ct);
  var row=await session.Connection!.QuerySingleOrDefaultAsync<NcrRow>(new CommandDefinition("""
    SELECT * FROM qms_core."Ncr" WHERE "NcrID"=@ncrId AND "QCFormID"=@formId FOR UPDATE
    """,new {ncrId,formId},session.Transaction,cancellationToken:ct))??throw new KeyNotFoundException("NCR was not found.");
  NcrRules.ValidateVerification(row,actor.UserID,request.ExpectedRevision);
  await session.Connection!.ExecuteAsync(new CommandDefinition("""
    UPDATE qms_core."Ncr" SET "VerifierUserID"=@actor,"VerifiedAt"=clock_timestamp(),"VerificationPassed"=@Passed,"VerificationComments"=@Comments,
     "ClosedAt"=CASE WHEN @Passed THEN clock_timestamp() ELSE NULL END,"Revision"="Revision"+1 WHERE "NcrID"=@ncrId
    """,new {actor=actor.UserID,request.Passed,request.Comments,ncrId},session.Transaction,cancellationToken:ct));
  await operation.CompleteAsync(request.Passed?"NCRVerifiedAndClosed":"NCRVerificationFailed",formId,new {ncrId,request.Passed},ct);
 }
}
public sealed record CorrectNcrRequest(string Severity,string Classification,bool IsBlocking,string Disposition,string CorrectiveAction,long CorrectiveEvidenceID,long ExpectedRevision);
public sealed record VerifyNcrRequest(bool Passed,string Comments,long ExpectedRevision);
public sealed record NcrRow
{
 public long NcrID {get;init;} public long ProjectID {get;init;} public long QCFormID {get;init;} public long QCResponseID {get;init;} public long QCFormFieldID {get;init;}
 public Guid CreatedByUserID {get;init;} public DateTimeOffset CreatedAt {get;init;} public string Severity {get;init;}="";public string Classification {get;init;}="";
 public bool? IsBlocking {get;init;} public string? Disposition {get;init;} public string? CorrectiveAction {get;init;} public Guid? CorrectedByUserID {get;init;}
 public long? CorrectiveEvidenceID {get;init;} public Guid? VerifierUserID {get;init;} public DateTimeOffset? VerifiedAt {get;init;} public bool? VerificationPassed {get;init;} public string? VerificationComments {get;init;} public DateTimeOffset? ClosedAt {get;init;} public long Revision {get;init;}
}
public static class NcrRules
{
 public static void ValidateCorrection(CorrectNcrRequest r)
 {
  if(new[]{r.Severity,r.Classification,r.Disposition,r.CorrectiveAction}.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>4000)||r.CorrectiveEvidenceID<=0||r.ExpectedRevision<0)
   throw new ArgumentException("Classification, severity, disposition, corrective action and valid evidence/revision are required.");
 }
 public static void ValidateVerification(NcrRow row,Guid verifier,long expectedRevision)
 {
  if(row.ClosedAt.HasValue || row.Revision!=expectedRevision)throw new InvalidOperationException("NCR is closed or changed.");
  if(row.CorrectedByUserID is null || string.IsNullOrWhiteSpace(row.CorrectiveAction)||!row.CorrectiveEvidenceID.HasValue||!row.IsBlocking.HasValue)throw new InvalidOperationException("NCR corrective action/evidence/classification is incomplete.");
  if(row.CorrectedByUserID==verifier || row.CreatedByUserID==verifier)throw new InvalidOperationException("Independent verification requires a different person from the creator and corrective actor.");
 }
}
