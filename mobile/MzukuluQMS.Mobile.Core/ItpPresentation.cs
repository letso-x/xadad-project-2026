using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
namespace MzukuluQMS.Mobile.Core;

public sealed record ItpListItem(long Id,string Number,string Title,int Revision,string Status,bool Current,string Progress);
public static class ItpPresentation
{
 public static IReadOnlyList<ItpListItem> List(JsonNode data)=>data.AsArray().Select(n=>new ItpListItem(n!["id"]!.GetValue<long>(),n["number"]!.ToString(),n["title"]!.ToString(),n["revision"]!.GetValue<int>(),n["status"]!.ToString(),n["isCurrent"]?.GetValue<bool>()==true,Progress(n["progress"]!))).ToList();
 public static string Progress(JsonNode p)=>$"{p["completedActivities"]}/{p["totalActivities"]} completed · {p["percentageComplete"]}% · {p["blockedActivities"]} blocked · {p["openHolds"]} open Holds · {p["releasedHolds"]} released · {p["witnessPending"]} Witness pending · {p["reviewPending"]} Review pending";
 public static string Point(string point)=>point switch{"Hold"=>"H · Hold","Witness"=>"W · Witness","Review"=>"R · Review",_=>"No quality point"};
 public static string AuditAction(string action)=>action switch
 {"ItpCreated"=>"Created ITP","ItpDraftUpdated"=>"Updated draft","ItpActivityAdded"=>"Added activity","ItpActivityEdited"=>"Edited activity","ItpActivityDeleted"=>"Removed draft activity","ItpActivityReordered"=>"Reordered activities","ItpDependencyChanged"=>"Changed dependencies","ItpRevisionCreated"=>"Created revision","Itp:submit"=>"Submitted for approval","Itp:approve"=>"Approved plan","Itp:complete"=>"Completed activity","Itp:release"=>"Released Hold","Itp:witness"=>"Recorded Witness","Itp:review"=>"Acknowledged Review","Itp:link-qc"=>"Linked QC record",_=>"ITP change"};
 public static string Availability(JsonNode activity)=>activity["blockedReason"]?.ToString()??(activity["execution"]?["completedAt"] is null?"Available":"Completed");
 public static IEnumerable<JsonNode> Ordered(JsonNode activities)=>activities.AsArray().Where(x=>x is not null).Select(x=>x!).OrderBy(x=>x["definition"]!["sequence"]!.GetValue<int>()).ThenBy(x=>x["definition"]!["id"]!.GetValue<long>());
 public static bool Can(string action,JsonNode version,JsonNode? activity,ISet<string> permissions,bool cached)
 {
  if(cached)return false;var status=version["status"]!.ToString();
  var permission=action switch{"edit" or "revise"=>"ManageItp","submit"=>"SubmitItp","approve"=>"ApproveItp","complete" or "link-qc"=>"ExecuteItp","release"=>"ReleaseItpHold","witness"=>"WitnessItp","review"=>"ReviewItp",_=>""};
  if(!permissions.Contains(permission))return false;
  if(action is "edit" or "submit")return status=="Draft";
  if(action=="approve")return status=="Submitted";
  if(status!="Approved"||version["isCurrent"]?.GetValue<bool>()!=true)return false;
  if(action=="revise")return true;
  if(activity is null||activity["blockedReason"] is not null)return false;
  var e=activity["execution"]!;var d=activity["definition"]!;
  return action switch{
   "complete"=>e["completedAt"] is null,
   "link-qc"=>e["completedAt"] is null&&d["checklistTemplateVersionId"] is not null,
   "release"=>d["pointType"]?.ToString()=="Hold"&&e["completedAt"] is not null&&e["releasedAt"] is null,
   "witness"=>d["pointType"]?.ToString()=="Witness"&&e["completedAt"] is not null&&e["witnessedAt"] is null,
   "review"=>d["pointType"]?.ToString()=="Review"&&e["completedAt"] is not null&&e["reviewedAt"] is null,_=>false};
 }
}
// Approved snapshots use the existing per-user encrypted payload store; never enter the QC outbox.
public static class ItpCache
{
 private static Guid Key(long project,long version)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"itp-approved:{project}:{version}")).AsSpan(0,16));
 public static async Task SaveAsync(OfflineStore store,long project,long version,JsonNode data)
 {
  if(data["projectId"]?.GetValue<long>()!=project||data["version"]?["id"]?.GetValue<long>()!=version||data["version"]?["status"]?.ToString()!="Approved")throw new ArgumentException("Only matching approved ITP snapshots may be cached here.");
  await store.CacheAsync(Key(project,version),version,data.ToJsonString());
 }
 public static async Task<JsonNode?> ReadAsync(OfflineStore store,long project,long version)
 {var record=await store.GetAsync(Key(project,version));return record is null?null:JsonNode.Parse(record.Json);}
}
