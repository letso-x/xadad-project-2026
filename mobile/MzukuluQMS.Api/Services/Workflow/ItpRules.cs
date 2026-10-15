namespace MzukuluQMS.Api.Services.Workflow;

public sealed record ItpDefinition(string Number,string Title,List<ItpStep> Activities);
public sealed record ItpStep(long Id,int Sequence,string Code,string Title,string Description,string Criteria,string PointType,
 List<long> DependsOn,long? ChecklistTemplateVersionId=null,bool RequireQc=false,bool WitnessRequired=true,bool IndependentHoldRelease=true);
public sealed record ItpExecution(long ActivityId,Guid? CompletedBy=null,DateTimeOffset? CompletedAt=null,string? CompletionComments=null,
 Guid? ReleasedBy=null,DateTimeOffset? ReleasedAt=null,string? ReleaseComments=null,
 Guid? WitnessedBy=null,DateTimeOffset? WitnessedAt=null,string? WitnessComments=null,
 Guid? ReviewedBy=null,DateTimeOffset? ReviewedAt=null,string? ReviewComments=null,long? QcFormId=null)
{ public ItpExecution():this(0){} }
public sealed record ItpVersion(long Id,long PlanId,int Revision,string Status,long Stamp,bool IsCurrent,ItpDefinition Definition,
 Guid CreatedBy,DateTimeOffset CreatedAt,Guid? SubmittedBy,DateTimeOffset? SubmittedAt,Guid? ApprovedBy,DateTimeOffset? ApprovedAt,
 List<ItpExecution> Executions);
public sealed record ItpWrite(long ExpectedStamp,ItpDefinition Definition);
public sealed record ItpAction(long ExpectedStamp,long? ActivityId=null,string? Comments=null,long? QcFormId=null);
public sealed record ItpProgress(int TotalActivities,int CompletedActivities,int BlockedActivities,int OpenHolds,int ReleasedHolds,int WitnessPending,int ReviewPending,decimal PercentageComplete);

public static class ItpRules
{
 public static void Validate(ItpDefinition d,bool submitting=false)
 {
  if(d is null||string.IsNullOrWhiteSpace(d.Number)||d.Number.Length>100||string.IsNullOrWhiteSpace(d.Title)||d.Title.Length>300)
   throw new ArgumentException("ITP number and title are required (100 and 300 characters maximum).");
  if(d.Activities is null||d.Activities.Count>500||submitting&&d.Activities.Count==0)throw new ArgumentException("Add at least one activity before submitting (maximum 500).");
  var a=d.Activities;
  if(a.Any(x=>x is null||x.DependsOn is null||x.Code is null||x.Description is null||x.Criteria is null))throw new ArgumentException("Activity details and dependencies are required.");
  if(a.Select(x=>x.Sequence).Distinct().Count()!=a.Count||a.Any(x=>x.Sequence<1)||a.Select(x=>x.Code.Trim().ToUpperInvariant()).Distinct().Count()!=a.Count)
   throw new ArgumentException("Activity order and codes must be unique.");
  foreach(var s in a)
  {
   if(s.Id<=0||string.IsNullOrWhiteSpace(s.Code)||s.Code.Length>100||string.IsNullOrWhiteSpace(s.Title)||s.Title.Length>300||s.Description.Length>4000||s.Criteria.Length>4000)
    throw new ArgumentException("Each activity needs a positive identifier, code and title; description and criteria may contain up to 4000 characters.");
   if(s.PointType is not ("None" or "Hold" or "Witness" or "Review"))throw new ArgumentException("Choose None, Hold, Witness or Review.");
   if(s.DependsOn is null||s.DependsOn.Contains(s.Id)||s.DependsOn.Distinct().Count()!=s.DependsOn.Count)throw new ArgumentException("An activity cannot depend on itself or repeat a dependency.");
   if(s.ChecklistTemplateVersionId is <=0||s.RequireQc&&s.ChecklistTemplateVersionId is null)throw new ArgumentException("Required QC must reference a checklist revision.");
   if(submitting&&s.PointType=="Hold"&&string.IsNullOrWhiteSpace(s.Criteria))throw new ArgumentException("Hold activities need explicit completion/release criteria.");
  }
  CoreRules.ValidateDependencies(a.Select(s=>new ItpActivity(s.Id,s.DependsOn,false,s.PointType=="Hold",false)).ToArray());
 }
 public static string? BlockedReason(ItpVersion v,ItpStep s)
 {
  foreach(var id in s.DependsOn)
  {
   var prior=v.Definition.Activities.Single(x=>x.Id==id);var e=v.Executions.Single(x=>x.ActivityId==id);
   if(e.CompletedAt is null||prior.PointType=="Hold"&&e.ReleasedAt is null||prior.PointType=="Witness"&&prior.WitnessRequired&&e.WitnessedAt is null||prior.PointType=="Review"&&e.ReviewedAt is null)
    return $"Complete and acknowledge/release Activity {prior.Code} before continuing.";
  }
  return null;
 }
 public static ItpProgress Progress(ItpVersion v)
 {
  var a=v.Definition.Activities;var n=a.Count;var complete=v.Executions.Count(x=>x.CompletedAt.HasValue);
  int Pending(string point,Func<ItpExecution,bool> pending)=>a.Count(s=>s.PointType==point&&pending(v.Executions.Single(e=>e.ActivityId==s.Id)));
  return new(n,complete,a.Count(s=>BlockedReason(v,s)!=null),Pending("Hold",e=>e.ReleasedAt is null),Pending("Hold",e=>e.ReleasedAt.HasValue),
   a.Count(s=>s.PointType=="Witness"&&s.WitnessRequired&&v.Executions.Single(e=>e.ActivityId==s.Id).WitnessedAt is null),Pending("Review",e=>e.ReviewedAt is null),n==0?0:Math.Round(100m*complete/n,1));
 }
 public static ItpVersion SaveDraft(ItpVersion v,ItpDefinition d)
 {
  if(v.Status!="Draft")throw new InvalidOperationException("Only a draft ITP can be edited. Create a revision of an approved plan.");
  Validate(d);return v with {Definition=d,Stamp=v.Stamp+1,Executions=d.Activities.Select(s=>new ItpExecution(s.Id)).ToList()};
 }
 public static ItpVersion Transition(ItpVersion v,string action,Guid actor,DateTimeOffset now)
 {
  Validate(v.Definition,true);
  return action switch
  {
   "submit" when v.Status=="Draft"=>v with {Status="Submitted",SubmittedBy=actor,SubmittedAt=now,Stamp=v.Stamp+1},
   "approve" when v.Status=="Submitted"=>v with {Status="Approved",ApprovedBy=actor,ApprovedAt=now,IsCurrent=true,Stamp=v.Stamp+1},
   _=>throw new InvalidOperationException("This ITP is not in the required state for this action.")
  };
 }
 public static ItpVersion Execute(ItpVersion v,string action,ItpAction r,Guid actor,DateTimeOffset now,bool qcAcceptable)
 {
  if(v.Status!="Approved"||!v.IsCurrent)throw new InvalidOperationException("Execute the current approved ITP revision only.");
  var step=v.Definition.Activities.SingleOrDefault(x=>x.Id==r.ActivityId)??throw new KeyNotFoundException("Activity was not found.");
  var e=v.Executions.Single(x=>x.ActivityId==step.Id);
  var blocked=BlockedReason(v,step);if(blocked!=null)throw new InvalidOperationException(blocked);
  if(string.IsNullOrWhiteSpace(r.Comments)||r.Comments.Length>4000)throw new ArgumentException("Record comments confirming the activity criteria (maximum 4000 characters).");
  if(action!="link-qc"&&step.RequireQc&&!qcAcceptable)throw new InvalidOperationException("Approve the linked QC record with current signatures and resolve its blocking NCRs before continuing.");
  e=action switch
  {
   "complete" when e.CompletedAt is null=>e with {CompletedBy=actor,CompletedAt=now,CompletionComments=r.Comments},
   "release" when step.PointType=="Hold"&&e.CompletedAt.HasValue&&e.ReleasedAt is null&&(!step.IndependentHoldRelease||e.CompletedBy!=actor)=>e with {ReleasedBy=actor,ReleasedAt=now,ReleaseComments=r.Comments},
   "witness" when step.PointType=="Witness"&&e.CompletedAt.HasValue&&e.WitnessedAt is null=>e with {WitnessedBy=actor,WitnessedAt=now,WitnessComments=r.Comments},
   "review" when step.PointType=="Review"&&e.CompletedAt.HasValue&&e.ReviewedAt is null=>e with {ReviewedBy=actor,ReviewedAt=now,ReviewComments=r.Comments},
   "link-qc" when e.CompletedAt is null&&r.QcFormId is >0&&step.ChecklistTemplateVersionId.HasValue=>e with {QcFormId=r.QcFormId},
   _=>throw new InvalidOperationException("This action is unavailable. Complete the activity first; independent Hold release requires a different authorised user.")
  };
  return v with {Stamp=v.Stamp+1,Executions=v.Executions.Select(x=>x.ActivityId==e.ActivityId?e:x).ToList()};
 }
 public static string Permission(string action)=>action switch
 {"create" or "save" or "revise"=>"ManageItp","submit"=>"SubmitItp","approve"=>"ApproveItp","complete" or "link-qc"=>"ExecuteItp","release"=>"ReleaseItpHold","witness"=>"WitnessItp","review"=>"ReviewItp",_=>throw new ArgumentException("Unknown ITP action.")};
}
