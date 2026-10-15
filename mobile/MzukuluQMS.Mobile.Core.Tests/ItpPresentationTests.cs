using System.Text.Json.Nodes;
using System.Security.Cryptography;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class ItpPresentationTests
{
 private static JsonNode Progress()=>JsonNode.Parse("""{"totalActivities":5,"completedActivities":2,"blockedActivities":2,"openHolds":1,"releasedHolds":0,"witnessPending":1,"reviewPending":1,"percentageComplete":40}""")!;
 [Fact]public void List_and_progress_map_real_counts()
 {
  var n=new JsonArray(new JsonObject{["id"]=7L,["number"]="ITP-A",["title"]="Plan",["revision"]=2,["status"]="Approved",["isCurrent"]=true,["progress"]=Progress()});var row=Assert.Single(ItpPresentation.List(n));Assert.Equal(7,row.Id);Assert.Equal("ITP-A",row.Number);Assert.Equal(2,row.Revision);Assert.True(row.Current);Assert.Contains("2/5 completed",row.Progress);Assert.Contains("40%",row.Progress);Assert.Contains("1 open Holds",row.Progress);
 }
 [Theory][InlineData("Hold","H · Hold")][InlineData("Witness","W · Witness")][InlineData("Review","R · Review")][InlineData("None","No quality point")]
 public void Point_labels(string type,string expected)=>Assert.Equal(expected,ItpPresentation.Point(type));
 [Fact]public void Ordering_and_business_reason_preserved()
 {
  var a=JsonNode.Parse("""[{"definition":{"id":2,"sequence":2},"execution":{},"blockedReason":"Complete and release Activity A1 before continuing."},{"definition":{"id":1,"sequence":1},"execution":{},"blockedReason":null}]""")!;var ordered=ItpPresentation.Ordered(a).ToArray();Assert.Equal(1,ordered[0]["definition"]!["id"]!.GetValue<int>());Assert.Equal("Available",ItpPresentation.Availability(ordered[0]));Assert.Contains("A1",ItpPresentation.Availability(ordered[1]));
 }
 [Theory][InlineData("edit","Draft","ManageItp")][InlineData("submit","Draft","SubmitItp")][InlineData("approve","Submitted","ApproveItp")][InlineData("revise","Approved","ManageItp")][InlineData("complete","Approved","ExecuteItp")][InlineData("release","Approved","ReleaseItpHold")][InlineData("witness","Approved","WitnessItp")][InlineData("review","Approved","ReviewItp")]
 public void Actions_require_permission_connection_and_current_state(string action,string status,string permission)
 {
  var v=new JsonObject{["status"]=status,["isCurrent"]=true};var a=JsonNode.Parse("""{"definition":{"pointType":"Hold"},"execution":{"completedAt":"2026-10-15"},"blockedReason":null}""")!;if(action=="complete")a["execution"]!["completedAt"]=null;if(action is "witness" or "review")a["definition"]!["pointType"]=action=="witness"?"Witness":"Review";
  var p=new HashSet<string>{permission};Assert.True(ItpPresentation.Can(action,v,a,p,false));Assert.False(ItpPresentation.Can(action,v,a,p,true));Assert.False(ItpPresentation.Can(action,v,a,new HashSet<string>(),false));if(action is "complete" or "release" or "witness" or "review"){a["blockedReason"]="Activity A1 incomplete";Assert.False(ItpPresentation.Can(action,v,a,p,false));}
 }
 private sealed class Keys:ILocalKeyStore {private readonly byte[] key=RandomNumberGenerator.GetBytes(32);public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(key.ToArray());}
 [Fact]public async Task Approved_cache_survives_restart_without_outbox_or_cross_owner_leak()
 {
  var path=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));var owner=Guid.NewGuid();var keys=new Keys();var data=JsonNode.Parse("""{"projectId":42,"version":{"id":9,"status":"Approved","revision":1},"activities":[{"definition":{"sequence":1,"code":"A1","pointType":"Hold"},"blockedReason":null}],"progress":{"totalActivities":1}}""")!;
  await using(var store=new OfflineStore(path,owner,keys)){await store.InitialiseAsync();await ItpCache.SaveAsync(store,42,9,data);Assert.Empty(await store.OutboxAsync());}
  await using(var reopened=new OfflineStore(path,owner,keys)){await reopened.InitialiseAsync();Assert.True(JsonNode.DeepEquals(data,await ItpCache.ReadAsync(reopened,42,9)));Assert.Null(await ItpCache.ReadAsync(reopened,43,9));}
  await using(var other=new OfflineStore(path,Guid.NewGuid(),keys)){await other.InitialiseAsync();Assert.Null(await ItpCache.ReadAsync(other,42,9));}
 }
 [Fact]public async Task Draft_or_mismatched_cache_cannot_be_mislabelled_approved()
 {
  var path=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));await using var store=new OfflineStore(path,Guid.NewGuid(),new Keys());await store.InitialiseAsync();var d=JsonNode.Parse("""{"projectId":42,"version":{"id":9,"status":"Draft"}}""")!;await Assert.ThrowsAsync<ArgumentException>(()=>ItpCache.SaveAsync(store,42,9,d));d["version"]!["status"]="Approved";await Assert.ThrowsAsync<ArgumentException>(()=>ItpCache.SaveAsync(store,42,8,d));
 }
}
