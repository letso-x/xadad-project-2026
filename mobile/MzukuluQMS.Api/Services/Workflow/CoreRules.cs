namespace MzukuluQMS.Api.Services.Workflow;
// Domain foundations awaiting inspected ITP/calibration persistence and endpoint integration.
public sealed record ItpActivity(long Id,IReadOnlyList<long> DependsOn,bool Completed,bool HoldRequired,bool HoldReleased);
public static class CoreRules
{
 public static void ValidateDependencies(IReadOnlyList<ItpActivity> activities)
 {
  if(activities.Select(a=>a.Id).Distinct().Count()!=activities.Count)throw new ArgumentException("Activity identifiers must be unique.");
  var map=activities.ToDictionary(a=>a.Id);var visiting=new HashSet<long>();var complete=new HashSet<long>();
  void Visit(long id)
  {
   if(!map.ContainsKey(id))throw new ArgumentException("Dependency refers to a missing activity.");
   if(complete.Contains(id))return;if(!visiting.Add(id))throw new ArgumentException("Dependency cycle is not permitted.");
   foreach(var predecessor in map[id].DependsOn)Visit(predecessor);visiting.Remove(id);complete.Add(id);
  }
  foreach(var activity in activities)Visit(activity.Id);
 }
 public static void RequireProgressPrerequisites(long activityId,IReadOnlyList<ItpActivity> activities)
 {
  ValidateDependencies(activities);var activity=activities.SingleOrDefault(a=>a.Id==activityId)??throw new KeyNotFoundException("Activity not found.");
  foreach(var dependency in activity.DependsOn)
  {var prior=activities.Single(a=>a.Id==dependency);if(!prior.Completed || prior.HoldRequired&&!prior.HoldReleased)throw new InvalidOperationException("Activity prerequisite is incomplete or held.");}
 }
 public static void RequireHoldRelease(long activityId,IReadOnlyList<ItpActivity> activities,bool permitted,string comments)
 {
  if(!permitted)throw new MzukuluQMS.Api.Exceptions.ForbiddenException("Hold release authority is required.");
  if(string.IsNullOrWhiteSpace(comments))throw new ArgumentException("Release comments are required.");
  RequireProgressPrerequisites(activityId,activities);var activity=activities.Single(a=>a.Id==activityId);
  if(!activity.HoldRequired || activity.HoldReleased)throw new InvalidOperationException("Activity is not awaiting Hold release.");
 }
 public static void RequireValidCalibration(DateOnly calibratedOn,DateOnly dueOn,DateOnly inspectionDate,bool inCalibration)
 {
  // Explicit engineering boundary: valid through due date, inclusive; later dates fail.
  if(!inCalibration || dueOn<calibratedOn || inspectionDate<calibratedOn || inspectionDate>dueOn)
   throw new InvalidOperationException("Instrument is not calibrated for the inspection date.");
 }
}
