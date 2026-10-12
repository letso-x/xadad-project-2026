using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;
public sealed class QualityReporting(QmsDbSession session,IQCWorkflow workflow,IQCFormContentHashService hash,ICurrentUserService users)
{
 public async Task<object> DashboardAsync(long projectId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReadReports",ct);
  var forms=(await session.Connection!.QueryAsync<StatusCount>(new CommandDefinition("""
   SELECT "Status"::text AS "Status",COUNT(*) AS "Count" FROM public."QCForm" WHERE "ProjectID"=@projectId GROUP BY "Status" ORDER BY "Status"::text
   """,new {projectId},session.Transaction,cancellationToken:ct))).AsList();
  var ncrs=await session.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("""
   SELECT COUNT(*) FROM qms_core."Ncr" WHERE "ProjectID"=@projectId AND "ClosedAt" IS NULL
   """,new {projectId},session.Transaction,cancellationToken:ct));
  return new {projectId,asOf=DateTimeOffset.UtcNow,qcStatusCounts=forms,openNcrCount=ncrs,limitations=new[]{"ITP, Hold, calibration, training and portfolio metrics are not implemented."}};
 }
 public async Task<HandoverReadiness> ReadinessAsync(long projectId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReadHandover",ct);
  var required=(await session.Connection!.QueryAsync<RequiredRecord>(new CommandDefinition("""
   SELECT "ChecklistTemplateVersionID","RequiredCount" FROM qms_core."HandoverRequirement" WHERE "ProjectID"=@projectId ORDER BY "ChecklistTemplateVersionID"
   """,new {projectId},session.Transaction,cancellationToken:ct))).AsList();
  var reasons=new List<string>();if(required.Count==0)reasons.Add("Required-record matrix has not been configured.");
  foreach(var requirement in required)
  {
   var approved=(await session.Connection!.QueryAsync<long>(new CommandDefinition("""
    SELECT "QCFormID" FROM public."QCForm" WHERE "ProjectID"=@projectId AND "ChecklistTemplateVersionID"=@version AND "Status"::text='Approved' ORDER BY "QCFormID"
    """,new {projectId,version=requirement.ChecklistTemplateVersionID},session.Transaction,cancellationToken:ct))).AsList();
   var valid=0;
   foreach(var formId in approved)
   {
    try{await workflow.EnsureCurrentSignaturesAsync(formId,await hash.GenerateHashAsync(formId,ct),ct);valid++;}
    catch(InvalidOperationException){reasons.Add("QC form "+formId+" has missing/stale signatures or an unresolved blocking NCR.");}
   }
   if(valid<requirement.RequiredCount)reasons.Add($"Template version {requirement.ChecklistTemplateVersionID}: {valid}/{requirement.RequiredCount} approved records with current signatures.");
  }
  var blocking=await session.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("""
   SELECT COUNT(*) FROM qms_core."Ncr" WHERE "ProjectID"=@projectId AND "ClosedAt" IS NULL AND COALESCE("IsBlocking",true)
   """,new {projectId},session.Transaction,cancellationToken:ct));
  if(blocking>0)reasons.Add(blocking+" unresolved blocking/unclassified NCR(s).");
  // Fail closed until every mandatory module's completeness gate is integrated.
  reasons.Add("ITP, controlled-document, calibration, training and evidence completeness gates are incomplete; final dossier release is unavailable.");
  return new(projectId,false,reasons,DateTimeOffset.UtcNow);
 }
 public async Task<IReadOnlyList<NotificationRow>> NotificationsAsync(CancellationToken ct)
 {
  var actor=await users.GetCurrentUserAsync(ct);await using var lease=await session.OpenAsync(ct);
  return (await lease.Connection.QueryAsync<NotificationRow>(new CommandDefinition("""
   SELECT n."NotificationID",n."QCFormID",n."Kind",n."CreatedAt",n."ReadAt" FROM qms_core."Notification" n
   JOIN public."QCForm" q ON q."QCFormID"=n."QCFormID"
   JOIN qms_security."ProjectAccess" pa ON pa."UserID"=@actor AND pa."ProjectID"=q."ProjectID"
   JOIN public."User" u ON u."UserID"=@actor AND u."IsActive"
   JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text AND rp."Permission"='ReadInspection'
   WHERE n."UserID"=@actor ORDER BY n."NotificationID" DESC LIMIT 100
   """,new {actor=actor.UserID},cancellationToken:ct))).AsList();
 }
 public async Task MarkReadAsync(long notificationId,CancellationToken ct)
 {
  var actor=await users.GetCurrentUserAsync(ct);await using var lease=await session.OpenAsync(ct);
  var count=await lease.Connection.ExecuteAsync(new CommandDefinition("""
   UPDATE qms_core."Notification" n SET "ReadAt"=COALESCE("ReadAt",clock_timestamp())
   FROM public."QCForm" q,public."User" u,qms_security."ProjectAccess" pa,qms_security."RolePermission" rp
   WHERE n."NotificationID"=@notificationId AND n."UserID"=@actor AND q."QCFormID"=n."QCFormID"
    AND u."UserID"=@actor AND u."IsActive" AND pa."UserID"=u."UserID" AND pa."ProjectID"=q."ProjectID"
    AND rp."RoleName"=u."Role"::text AND rp."Permission"='ReadInspection'
   """,new {notificationId,actor=actor.UserID},cancellationToken:ct));
  if(count!=1)throw new KeyNotFoundException("Notification was not found or is not accessible.");
 }
 private sealed record RequiredRecord {public long ChecklistTemplateVersionID {get;init;} public int RequiredCount {get;init;}}
}
public sealed record HandoverReadiness(long ProjectID,bool IsReady,IReadOnlyList<string> Reasons,DateTimeOffset AsOf);
public sealed record StatusCount(string Status,long Count);
public sealed record NotificationRow {public long NotificationID {get;init;}public long QCFormID {get;init;}public string Kind {get;init;}="";public DateTimeOffset CreatedAt {get;init;}public DateTimeOffset? ReadAt {get;init;}}
