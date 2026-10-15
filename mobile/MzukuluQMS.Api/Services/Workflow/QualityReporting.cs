using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;
public sealed class QualityReporting(QmsDbSession session,IQCWorkflow workflow,IQCFormContentHashService hash,ICurrentUserService users)
{
 public async Task<System.Text.Json.JsonElement> PortfolioAsync(CancellationToken ct)
 {
  var actor=await users.GetCurrentUserAsync(ct);await using var lease=await session.OpenAsync(ct);
  var json=await lease.Connection.QuerySingleAsync<string>(new CommandDefinition("""
   WITH permissions AS (
    SELECT rp."Permission" FROM public."User" u JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text
    WHERE u."UserID"=@id AND u."IsActive"
   ), projects AS (
    SELECT p.* FROM public."Project" p JOIN qms_security."ProjectAccess" pa ON pa."ProjectID"=p."ProjectID" AND pa."UserID"=@id
    WHERE EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadProjects')
   ), forms AS (
    SELECT q.* FROM public."QCForm" q JOIN projects p ON p."ProjectID"=q."ProjectID"
    WHERE EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadInspection')
   )
   SELECT jsonb_build_object(
    'asOf',clock_timestamp(),
    'totalProjects',(SELECT count(*) FROM projects),'activeProjects',(SELECT count(*) FROM projects WHERE "IsActive" AND "Status"='Active'),
    'totalQcRecords',CASE WHEN EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadInspection') THEN (SELECT count(*) FROM forms) END,
    'formsInProgress',(SELECT count(*) FROM forms WHERE "Status"::text IN('Draft','InProgress','ReturnedForCorrection')),
    'approvedForms',(SELECT count(*) FROM forms WHERE "Status"::text='Approved'),
    'openNcrCount',CASE WHEN EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadNcr') THEN
      ((SELECT count(*) FROM qms_core."Ncr" n JOIN projects p ON p."ProjectID"=n."ProjectID" WHERE n."ClosedAt" IS NULL)
      +(SELECT count(*) FROM public."NCR" legacy JOIN public."QCForm" q ON q."QCFormID"=legacy."QCFormID" JOIN projects p ON p."ProjectID"=q."ProjectID"
        WHERE legacy."ClosedAt" IS NULL AND (NOT EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID")
          OR EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID" AND n."ClosedAt" IS NOT NULL)))) END,
    'clients',CASE WHEN EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadClients') THEN
      (SELECT count(DISTINCT "ClientID") FROM projects) END,
    'activeTemplates',CASE WHEN EXISTS(SELECT 1 FROM permissions WHERE "Permission"='ReadTemplates') THEN
      (SELECT count(*) FROM public."ChecklistTemplate" t WHERE t."IsActive" AND EXISTS(
        SELECT 1 FROM public."ChecklistTemplateVersion" v WHERE v."ChecklistTemplateID"=t."ChecklistTemplateID" AND v."Status"::text='Active')) END,
    'recentActivity',COALESCE((SELECT jsonb_agg(to_jsonb(recent)) FROM (
      SELECT "QCFormID" AS "qcFormID","FormNumber" AS "formNumber","Status"::text AS "status","UpdatedAt" AS "updatedAt"
      FROM forms ORDER BY "UpdatedAt" DESC,"QCFormID" DESC LIMIT 10) recent),'[]'::jsonb)
   )::text
   """,new {id=actor.UserID},cancellationToken:ct));
  using var document=System.Text.Json.JsonDocument.Parse(json);return document.RootElement.Clone();
 }
 public async Task<object> DashboardAsync(long projectId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReadReports",ct);
  var forms=(await session.Connection!.QueryAsync<StatusCount>(new CommandDefinition("""
   SELECT "Status"::text AS "Status",COUNT(*) AS "Count" FROM public."QCForm" WHERE "ProjectID"=@projectId GROUP BY "Status" ORDER BY "Status"::text
   """,new {projectId},session.Transaction,cancellationToken:ct))).AsList();
  var ncrs=await session.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("""
   SELECT (SELECT COUNT(*) FROM qms_core."Ncr" WHERE "ProjectID"=@projectId AND "ClosedAt" IS NULL)
     +(SELECT COUNT(*) FROM public."NCR" legacy JOIN public."QCForm" q ON q."QCFormID"=legacy."QCFormID" WHERE q."ProjectID"=@projectId AND legacy."ClosedAt" IS NULL
      AND (NOT EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID")
        OR EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID" AND n."ClosedAt" IS NOT NULL)))
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
  var historical=await session.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("""
   SELECT count(*) FROM public."NCR" legacy JOIN public."QCForm" q ON q."QCFormID"=legacy."QCFormID"
   WHERE q."ProjectID"=@projectId AND legacy."ClosedAt" IS NULL
    AND (NOT EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID")
      OR EXISTS(SELECT 1 FROM qms_core."Ncr" n WHERE n."LegacyNCRID"=legacy."NCRID" AND n."ClosedAt" IS NOT NULL))
   """,new {projectId},session.Transaction,cancellationToken:ct));
  if(historical>0)reasons.Add(historical+" historical/unreconciled or inconsistent NCR(s).");
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
