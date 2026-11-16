using System.Text.Json;
using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.SignOffs;
namespace MzukuluQMS.Api.Services.Workflow;

// All mutations share the established permission/project-access transaction and lock the plan.
public sealed class ItpService(QmsDbSession db,IQCWorkflow workflow,ICurrentUserService users,IQCFormContentHashService hash)
{
 private CommandDefinition Sql(string sql,object? args,CancellationToken ct)=>new(sql,args,db.Transaction,cancellationToken:ct);
 private async Task Schema(CancellationToken ct)
 {
  if(!await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT to_regclass('qms_core.\"ItpExecution\"') IS NOT NULL",null,ct)))
   throw new InvalidOperationException("ITP needs administrator setup. Apply migration 007 and deploy the matching API. Saved QC work is unaffected.");
 }
 private async Task<Guid> Begin(long project,string permission,CancellationToken ct)
 {
  await workflow.BeginAsync(null,project,permission,ct);await Schema(ct);return (await users.GetCurrentUserAsync(ct)).UserID;
 }
 private async Task<ItpVersion> Load(long project,long id,bool locking,CancellationToken ct)
 {
  var header=await db.Connection!.QuerySingleOrDefaultAsync<Header>(Sql("""
   SELECT v.*,p."Number",p."CurrentVersionId" FROM qms_core."ItpVersion" v JOIN qms_core."ItpPlan" p ON p."Id"=v."PlanId"
   WHERE v."Id"=@id AND p."ProjectId"=@project
   """+(locking?" FOR UPDATE OF p,v":" FOR SHARE OF p,v"),new{project,id},ct))??throw new KeyNotFoundException("ITP revision was not found in this project.");
  var steps=(await db.Connection!.QueryAsync<StepRow>(Sql("SELECT * FROM qms_core.\"ItpActivity\" WHERE \"VersionId\"=@id ORDER BY \"Sequence\",\"Id\"",new{id},ct))).ToList();
  var dependencies=(await db.Connection!.QueryAsync<DependencyRow>(Sql("SELECT \"PredecessorId\",\"SuccessorId\" FROM qms_core.\"ItpDependency\" WHERE \"VersionId\"=@id",new{id},ct))).ToList();
  var executions=(await db.Connection!.QueryAsync<ItpExecution>(Sql("SELECT \"ActivityId\",\"CompletedBy\",\"CompletedAt\",\"CompletionComments\",\"ReleasedBy\",\"ReleasedAt\",\"ReleaseComments\",\"WitnessedBy\",\"WitnessedAt\",\"WitnessComments\",\"ReviewedBy\",\"ReviewedAt\",\"ReviewComments\",\"QcFormId\" FROM qms_core.\"ItpExecution\" WHERE \"VersionId\"=@id",new{id},ct))).ToList();
  return new(header.Id,header.PlanId,header.Revision,header.Status,header.Stamp,header.CurrentVersionId==id,
   new(header.Number,header.Title,steps.Select(s=>new ItpStep(s.Id,s.Sequence,s.Code,s.Title,s.Description,s.Criteria,s.PointType,dependencies.Where(d=>d.SuccessorId==s.Id).Select(d=>d.PredecessorId).Order().ToList(),s.ChecklistTemplateVersionId,s.RequireQc,s.WitnessRequired,s.IndependentHoldRelease)).ToList()),
   header.CreatedBy,header.CreatedAt,header.SubmittedBy,header.SubmittedAt,header.ApprovedBy,header.ApprovedAt,executions);
 }
 public async Task<object> List(long project,CancellationToken ct)
 {
  await Begin(project,"ReadItp",ct);
  var ids=await db.Connection!.QueryAsync<long>(Sql("SELECT v.\"Id\" FROM qms_core.\"ItpVersion\" v JOIN qms_core.\"ItpPlan\" p ON p.\"Id\"=v.\"PlanId\" WHERE p.\"ProjectId\"=@project ORDER BY p.\"Number\",v.\"Revision\" DESC",new{project},ct));
  var list=new List<object>();foreach(var id in ids){var v=await Load(project,id,false,ct);list.Add(new{v.Id,v.PlanId,v.Revision,v.Status,v.Stamp,v.IsCurrent,v.Definition.Number,v.Definition.Title,progress=ItpRules.Progress(v)});}return list;
 }
 public async Task<object> ProjectProgress(long project,CancellationToken ct)
 {await Begin(project,"ReadItp",ct);return (await ProjectProgressInTransaction(project,ct))!;}
 internal async Task<object?> ProjectProgressInTransaction(long project,CancellationToken ct)
 {
  var actor=(await users.GetCurrentUserAsync(ct)).UserID;
  if(!await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT EXISTS(SELECT 1 FROM public.\"User\" u JOIN qms_security.\"RolePermission\" rp ON rp.\"RoleName\"=u.\"Role\"::text WHERE u.\"UserID\"=@actor AND u.\"IsActive\" AND rp.\"Permission\"='ReadItp')",new{actor},ct)))return null;
  if(!await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT to_regclass('qms_core.\"ItpExecution\"') IS NOT NULL",null,ct)))return new{available=false,reason="ITP migration 007 requires manual application."};
  var ids=(await db.Connection!.QueryAsync<long>(Sql("SELECT \"CurrentVersionId\" FROM qms_core.\"ItpPlan\" WHERE \"ProjectId\"=@project AND \"CurrentVersionId\" IS NOT NULL ORDER BY \"Id\"",new{project},ct))).ToArray();
  var counts=new List<ItpProgress>();foreach(var id in ids)counts.Add(ItpRules.Progress(await Load(project,id,false,ct)));
  var total=counts.Sum(x=>x.TotalActivities);var done=counts.Sum(x=>x.CompletedActivities);
  return new{available=true,currentApprovedPlans=counts.Count,progress=new ItpProgress(total,done,counts.Sum(x=>x.BlockedActivities),counts.Sum(x=>x.OpenHolds),counts.Sum(x=>x.ReleasedHolds),counts.Sum(x=>x.WitnessPending),counts.Sum(x=>x.ReviewPending),total==0?0:Math.Round(100m*done/total,1))};
 }
 public async Task<object> Get(long project,long id,CancellationToken ct)
 {
  await Begin(project,"ReadItp",ct);var v=await Load(project,id,false,ct);
  var projectName=await db.Connection!.QuerySingleAsync<string>(Sql("SELECT \"ProjectName\" FROM public.\"Project\" WHERE \"ProjectID\"=@project",new{project},ct));
  var qcIds=v.Executions.Where(e=>e.QcFormId.HasValue).Select(e=>e.QcFormId!.Value).ToArray();
  var linked=qcIds.Length==0?[]:(await db.Connection!.QueryAsync<QcState>(Sql("""
   SELECT q."QCFormID" AS "Id",q."FormNumber" AS "Number",q."Status"::text AS "Status",
    (SELECT count(*) FROM qms_core."Ncr" n WHERE n."QCFormID"=q."QCFormID" AND n."ClosedAt" IS NULL AND COALESCE(n."IsBlocking",true))
    +(SELECT count(*) FROM public."NCR" n WHERE n."QCFormID"=q."QCFormID" AND n."ClosedAt" IS NULL AND
      NOT EXISTS(SELECT 1 FROM qms_core."Ncr" b WHERE b."LegacyNCRID"=n."NCRID" AND b."ClosedAt" IS NULL)) AS "BlockingNcrs"
   FROM public."QCForm" q WHERE q."QCFormID"=ANY(@qcIds) AND q."ProjectID"=@project
   """,new{qcIds,project},ct))).ToList();
  var names=(await db.Connection!.QueryAsync<NameRow>(Sql("SELECT \"UserID\" AS \"Id\",concat_ws(' ',\"FirstName\",\"LastName\") AS \"Name\" FROM public.\"User\" WHERE \"UserID\"=ANY(@ids)",new{ids=v.Executions.SelectMany(e=>new[]{e.CompletedBy,e.ReleasedBy,e.WitnessedBy,e.ReviewedBy}).Append(v.CreatedBy).Append(v.SubmittedBy).Append(v.ApprovedBy).Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().ToArray()},ct))).ToDictionary(x=>x.Id,x=>x.Name);
  return new{projectId=project,projectName,asOf=DateTimeOffset.UtcNow,version=v,progress=ItpRules.Progress(v),activities=v.Definition.Activities.Select(s=>new{definition=s,execution=v.Executions.Single(e=>e.ActivityId==s.Id),status=v.Executions.Single(e=>e.ActivityId==s.Id).CompletedAt.HasValue?"Completed":ItpRules.BlockedReason(v,s)!=null?"Blocked":"Available",blockedReason=ItpRules.BlockedReason(v,s),linkedQc=linked.SingleOrDefault(q=>q.Id==v.Executions.Single(e=>e.ActivityId==s.Id).QcFormId)}),actorNames=names};
 }
 private async Task CheckTemplates(ItpDefinition d,CancellationToken ct)
 {
  foreach(var id in d.Activities.Where(s=>s.ChecklistTemplateVersionId.HasValue).Select(s=>s.ChecklistTemplateVersionId!.Value).Distinct())
   if(!await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT EXISTS(SELECT 1 FROM public.\"ChecklistTemplateVersion\" WHERE \"ChecklistTemplateVersionID\"=@id)",new{id},ct)))throw new ArgumentException("The linked checklist revision does not exist.");
 }
 private async Task SaveDefinition(ItpVersion v,CancellationToken ct)
 {
  // Only called while the persisted revision is Draft. Delete dependencies/empty execution first, then rebuild draft rows atomically.
  await db.Connection!.ExecuteAsync(Sql("DELETE FROM qms_core.\"ItpDependency\" WHERE \"VersionId\"=@id; DELETE FROM qms_core.\"ItpExecution\" WHERE \"VersionId\"=@id; DELETE FROM qms_core.\"ItpActivity\" WHERE \"VersionId\"=@id",new{id=v.Id},ct));
  foreach(var s in v.Definition.Activities)
  {
   var args=new DynamicParameters(s);args.Add("version",v.Id);
   await db.Connection!.ExecuteAsync(Sql("""
    INSERT INTO qms_core."ItpActivity"("VersionId","Id","Sequence","Code","Title","Description","Criteria","PointType","ChecklistTemplateVersionId","RequireQc","WitnessRequired","IndependentHoldRelease")
    VALUES(@version,@Id,@Sequence,@Code,@Title,@Description,@Criteria,@PointType,@ChecklistTemplateVersionId,@RequireQc,@WitnessRequired,@IndependentHoldRelease);
    INSERT INTO qms_core."ItpExecution"("VersionId","ActivityId") VALUES(@version,@Id)
    """,args,ct));
  }
  foreach(var s in v.Definition.Activities)foreach(var predecessor in s.DependsOn)
   await db.Connection!.ExecuteAsync(Sql("INSERT INTO qms_core.\"ItpDependency\" VALUES(@version,@predecessor,@successor)",new{version=v.Id,predecessor,successor=s.Id},ct));
 }
 private async Task Audit(Guid actor,string action,long id,object details,CancellationToken ct)
 {
  await db.Connection!.ExecuteAsync(Sql("INSERT INTO qms_security.\"AuditEvent\"(\"ActorUserID\",\"Action\",\"EntityType\",\"EntityID\",\"Details\") VALUES(@actor,@action,'ITP',@id,CAST(@details AS jsonb))",new{actor,action,id,details=JsonSerializer.Serialize(details)},ct));
 }
 public async Task<object> Create(long project,ItpDefinition definition,CancellationToken ct)
 {
  ItpRules.Validate(definition);var actor=await Begin(project,ItpRules.Permission("create"),ct);await CheckTemplates(definition,ct);
  await db.Connection!.ExecuteAsync(Sql("SELECT pg_advisory_xact_lock(hashtextextended('itp:'||CAST(@project AS text),0))",new{project},ct));
  if(await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT EXISTS(SELECT 1 FROM qms_core.\"ItpPlan\" WHERE \"ProjectId\"=@project AND lower(btrim(\"Number\"))=lower(btrim(@number)))",new{project,number=definition.Number},ct)))throw new ConflictException("This ITP number already exists in the project.");
  var plan=await db.Connection!.QuerySingleAsync<long>(Sql("INSERT INTO qms_core.\"ItpPlan\"(\"ProjectId\",\"Number\") VALUES(@project,@number) RETURNING \"Id\"",new{project,number=definition.Number.Trim()},ct));
  var id=await NewVersion(plan,1,definition,actor,ct);await Audit(actor,"ItpCreated",id,new{definition},ct);await db.CommitAsync(ct);return new{id};
 }
 private async Task<long> NewVersion(long plan,int revision,ItpDefinition d,Guid actor,CancellationToken ct)
 {
  var id=await db.Connection!.QuerySingleAsync<long>(Sql("INSERT INTO qms_core.\"ItpVersion\"(\"PlanId\",\"Revision\",\"Title\",\"CreatedBy\") VALUES(@plan,@revision,@title,@actor) RETURNING \"Id\"",new{plan,revision,title=d.Title,actor},ct));
  await SaveDefinition(new(id,plan,revision,"Draft",0,false,d,actor,DateTimeOffset.UtcNow,null,null,null,null,[]),ct);return id;
 }
 public async Task<object> Save(long project,long id,ItpWrite r,CancellationToken ct)
 {
  ItpRules.Validate(r.Definition);
  var actor=await Begin(project,ItpRules.Permission("save"),ct);var old=await Load(project,id,true,ct);Stamp(old,r.ExpectedStamp);
  if(old.Definition.Number!=r.Definition.Number)throw new ArgumentException("The ITP number is fixed; edit its title and activities instead.");
  var next=ItpRules.SaveDraft(old,r.Definition);await CheckTemplates(next.Definition,ct);await SaveDefinition(next,ct);
  await db.Connection!.ExecuteAsync(Sql("UPDATE qms_core.\"ItpVersion\" SET \"Title\"=@title,\"Stamp\"=@stamp WHERE \"Id\"=@id",new{id,title=next.Definition.Title,stamp=next.Stamp},ct));
  await Audit(actor,"ItpDraftUpdated",id,new{previous=old.Definition,current=next.Definition},ct);
  foreach(var s in next.Definition.Activities)
  {
   var prior=old.Definition.Activities.SingleOrDefault(x=>x.Id==s.Id);
   if(prior is null||JsonSerializer.Serialize(prior)!=JsonSerializer.Serialize(s))await Audit(actor,prior is null?"ItpActivityAdded":"ItpActivityEdited",id,new{previous=prior,current=s},ct);
   if(prior!=null&&prior.Sequence!=s.Sequence)await Audit(actor,"ItpActivityReordered",id,new{previous=prior.Sequence,current=s.Sequence,s.Id},ct);
   if(prior!=null&&!prior.DependsOn.SequenceEqual(s.DependsOn))await Audit(actor,"ItpDependencyChanged",id,new{s.Id,previous=prior.DependsOn,current=s.DependsOn},ct);
  }
  foreach(var s in old.Definition.Activities.Where(x=>!next.Definition.Activities.Any(y=>y.Id==x.Id)))await Audit(actor,"ItpActivityDeleted",id,new{previous=s},ct);
  await db.CommitAsync(ct);return new{id,stamp=next.Stamp};
 }
 public async Task<object> Act(long project,long id,string action,ItpAction r,CancellationToken ct)
 {
  if(r.Comments?.Length>4000)throw new ArgumentException("Comments may contain up to 4000 characters.");
  var actor=await Begin(project,ItpRules.Permission(action),ct);var old=await Load(project,id,true,ct);Stamp(old,r.ExpectedStamp);
  if(action=="revise")
  {
   if(old.Status!="Approved"||!old.IsCurrent)throw new InvalidOperationException("Create a revision from the current approved plan.");
   if(await db.Connection!.ExecuteScalarAsync<bool>(Sql("SELECT EXISTS(SELECT 1 FROM qms_core.\"ItpVersion\" WHERE \"PlanId\"=@plan AND \"Status\"<>'Approved')",new{plan=old.PlanId},ct)))throw new ConflictException("A pending revision already exists. Open it to continue.");
   var revision=old.Revision+1;var created=await NewVersion(old.PlanId,revision,old.Definition,actor,ct);await Audit(actor,"ItpRevisionCreated",created,new{sourceVersion=id,revision,definition=old.Definition},ct);await db.CommitAsync(ct);return new{id=created};
  }
  ItpVersion next;
  if(action is "submit" or "approve"){await CheckTemplates(old.Definition,ct);next=ItpRules.Transition(old,action,actor,DateTimeOffset.UtcNow);}
  else
  {
   var step=old.Definition.Activities.SingleOrDefault(x=>x.Id==r.ActivityId)??throw new KeyNotFoundException("Activity was not found.");
   var execution=old.Executions.Single(x=>x.ActivityId==step.Id);var qc=action=="link-qc"?r.QcFormId:execution.QcFormId;var acceptable=false;
   if(qc.HasValue)
   {
    var linked=await db.Connection!.QuerySingleOrDefaultAsync<QcLink>(Sql("SELECT \"Status\"::text AS \"Status\",\"ChecklistTemplateVersionID\" AS \"Template\" FROM public.\"QCForm\" WHERE \"QCFormID\"=@qc AND \"ProjectID\"=@project FOR SHARE",new{qc,project},ct))??throw new ArgumentException("Choose a QC record from the same project.");
    if(linked.Template!=step.ChecklistTemplateVersionId)throw new ArgumentException("The QC record must use the configured checklist revision.");
    acceptable=linked.Status=="Approved";
    if(acceptable&&action!="link-qc")await workflow.EnsureCurrentSignaturesAsync(qc.Value,await hash.GenerateHashAsync(qc.Value,ct),ct);
   }
   next=ItpRules.Execute(old,action,r,actor,DateTimeOffset.UtcNow,acceptable);
   var changed=next.Executions.Single(x=>x.ActivityId==r.ActivityId);var args=new DynamicParameters(changed);args.Add("id",id);
   await db.Connection!.ExecuteAsync(Sql("""
    UPDATE qms_core."ItpExecution" SET "CompletedBy"=@CompletedBy,"CompletedAt"=@CompletedAt,"CompletionComments"=@CompletionComments,
    "ReleasedBy"=@ReleasedBy,"ReleasedAt"=@ReleasedAt,"ReleaseComments"=@ReleaseComments,"WitnessedBy"=@WitnessedBy,"WitnessedAt"=@WitnessedAt,"WitnessComments"=@WitnessComments,
    "ReviewedBy"=@ReviewedBy,"ReviewedAt"=@ReviewedAt,"ReviewComments"=@ReviewComments,"QcFormId"=@QcFormId WHERE "VersionId"=@id AND "ActivityId"=@ActivityId
    """,args,ct));
  }
  await db.Connection!.ExecuteAsync(Sql("""
   UPDATE qms_core."ItpVersion" SET "Stamp"=@Stamp,"Status"=@Status,"SubmittedBy"=@SubmittedBy,"SubmittedAt"=@SubmittedAt,"ApprovedBy"=@ApprovedBy,"ApprovedAt"=@ApprovedAt WHERE "Id"=@Id
   """,next,ct));
  if(action=="approve")await db.Connection!.ExecuteAsync(Sql("UPDATE qms_core.\"ItpPlan\" SET \"CurrentVersionId\"=@id WHERE \"Id\"=@plan",new{id,plan=old.PlanId},ct));
  await Audit(actor,"Itp:"+action,id,new{previous=old,current=next,comments=r.Comments},ct);await db.CommitAsync(ct);return new{id,stamp=next.Stamp};
 }
 private static void Stamp(ItpVersion v,long expected){if(v.Stamp!=expected)throw new ConflictException("This ITP changed. Your draft is retained; reload before continuing.");}
 public async Task<object> AuditHistory(long project,long id,CancellationToken ct)
 {
  await Begin(project,"ReadItp",ct);await Load(project,id,false,ct);
  return (await db.Connection!.QueryAsync(Sql("""
   SELECT a."AuditEventID" AS "id",a."Action" AS "action",a."OccurredAt" AS "occurredAt",a."Details" AS "details",concat_ws(' ',u."FirstName",u."LastName") AS "actorName"
   FROM qms_security."AuditEvent" a JOIN public."User" u ON u."UserID"=a."ActorUserID"
   WHERE a."EntityType"='ITP' AND a."EntityID"=@id ORDER BY a."AuditEventID" DESC LIMIT 200
   """,new{id},ct))).ToList();
 }
 private sealed class Header
 {
  public long Id{get;set;} public long PlanId{get;set;} public int Revision{get;set;} public string Title{get;set;}="";public string Number{get;set;}="";public string Status{get;set;}="";public long Stamp{get;set;}public long? CurrentVersionId{get;set;}
  public Guid CreatedBy{get;set;}public DateTimeOffset CreatedAt{get;set;}public Guid? SubmittedBy{get;set;}public DateTimeOffset? SubmittedAt{get;set;}public Guid? ApprovedBy{get;set;}public DateTimeOffset? ApprovedAt{get;set;}
 }
 private sealed class StepRow
 {
  public long Id{get;set;}public int Sequence{get;set;}public string Code{get;set;}="";public string Title{get;set;}="";public string Description{get;set;}="";public string Criteria{get;set;}="";public string PointType{get;set;}="None";public long? ChecklistTemplateVersionId{get;set;}public bool RequireQc{get;set;}public bool WitnessRequired{get;set;}public bool IndependentHoldRelease{get;set;}
 }
 private sealed record DependencyRow(long PredecessorId,long SuccessorId);
 private sealed record NameRow(Guid Id,string Name);
 private sealed record QcState(long Id,string Number,string Status,long BlockingNcrs);
 private sealed record QcLink(string Status,long Template);
}
