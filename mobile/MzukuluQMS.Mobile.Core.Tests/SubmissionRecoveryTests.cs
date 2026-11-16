using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;

public sealed class SubmissionRecoveryTests
{
 sealed class Keys:ILocalKeyStore {public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(new byte[32]);}
 sealed class Session(Guid owner):ISyncSession {public Guid? Owner=>owner;public Task<string?> GetAccessTokenAsync(CancellationToken ct)=>Task.FromResult<string?>("test");}
 sealed class Transport(int status):ISyncTransport
 {
  public List<PendingOperation> Sent=[];
  public Task<SyncReply> SendAsync(PendingOperation op,CancellationToken ct)
  {Sent.Add(op);return Task.FromResult(new SyncReply(status,"",op.ExpectedRevision+1,null));}
 }
 const string Form="""{"qcFormID":10,"projectID":2,"status":"InProgress","revision":4,"value":"retained"}""";

 [Theory][InlineData(204,"Submitted")][InlineData(403,"Sync failed")][InlineData(409,"Conflict")][InlineData(500,"Pending sync")][InlineData(401,"Pending sync")]
 public async Task Only_acknowledged_submit_changes_status_and_confirms_navigation(int code,string display)
 {
  var owner=Guid.NewGuid();var entity=Guid.NewGuid();
  await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),owner,new Keys());
  await store.CacheAsync(entity,10,Form);
  var intent=await store.SaveAndEnqueueAsync(entity,Form,"SubmitQCForm","{}",4);
  await new SyncEngine(store,new Session(owner),new Transport(code)).RunAsync();
  var record=(await store.GetAsync(entity))!;var outbox=await store.OutboxAsync();
  Assert.Equal(code==204,RecordSyncState.SubmissionConfirmed(intent,outbox));
  Assert.Equal(display,RecordSyncState.Describe(record,outbox));
  Assert.Equal(code==204?"Submitted":"InProgress",JsonNode.Parse(record.Json)!["status"]!.ToString());
  Assert.Equal("retained",JsonNode.Parse(record.Json)!["value"]!.ToString());
 }

 [Fact]public async Task Failed_field_write_blocks_submit_and_survives_restart_until_recovered()
 {
  var directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());var owner=Guid.NewGuid();var entity=Guid.NewGuid();var keys=new Keys();Guid intent;
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   await store.CacheAsync(entity,10,Form);
   await store.SaveAndEnqueueAsync(entity,Form,"UpdateQCResponse","{}",4);
   intent=await store.SaveAndEnqueueAsync(entity,Form,"SubmitQCForm","{}",4);
   var offline=new Transport(401);await new SyncEngine(store,new Session(owner),offline).RunAsync();
   Assert.Single(offline.Sent);Assert.False(RecordSyncState.SubmissionConfirmed(intent,await store.OutboxAsync()));
  }
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   // Respect the durable first-retry backoff, then recover through transport.
   await Task.Delay(TimeSpan.FromSeconds(2.2));
   var online=new Transport(204);await new SyncEngine(store,new Session(owner),online).RunAsync();
   Assert.Equal(new[]{"UpdateQCResponse","SubmitQCForm"},online.Sent.Select(o=>o.Kind));
   Assert.Equal(5,online.Sent[1].ExpectedRevision);
   Assert.True(RecordSyncState.SubmissionConfirmed(intent,await store.OutboxAsync()));
  }
 }

 [Fact]public async Task New_edit_uses_acknowledged_revision_without_overwriting_entered_value()
 {
  var entity=Guid.NewGuid();await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),Guid.NewGuid(),new Keys());
  await store.CacheAsync(entity,10,Form);await store.SaveAndEnqueueAsync(entity,Form,"UpdateQCResponse","{}",4);
  await store.CompleteAsync((await store.ClaimAsync())!,null,5);
  var stale=JsonNode.Parse(Form)!;stale["value"]="next edit";
  await store.SaveAndEnqueueAsync(entity,stale.ToJsonString(),"UpdateQCResponse","next",4);
  Assert.Equal(5,(await store.ClaimAsync())!.ExpectedRevision);
  Assert.Equal("next edit",JsonNode.Parse((await store.GetAsync(entity))!.Json)!["value"]!.ToString());
 }

 [Fact]public void Unrelated_success_never_confirms_submission()
 {
  var id=Guid.NewGuid();var entry=new OutboxEntry(new(id,Guid.NewGuid(),"CorrectNcr","{}",1,0,null),"Completed",null);
  Assert.False(RecordSyncState.SubmissionConfirmed(id,new[]{entry}));
 }
 [Theory]
 [InlineData("CorrectNcr","Pending",false)][InlineData("CorrectNcr","Failed",false)][InlineData("CorrectNcr","Conflict",false)][InlineData("CorrectNcr","Completed",true)]
 [InlineData("VerifyNcr","Pending",false)][InlineData("VerifyNcr","Failed",false)][InlineData("VerifyNcr","Conflict",false)][InlineData("VerifyNcr","Completed",true)]
 public void NCR_confirmation_uses_its_own_acknowledgment(string kind,string status,bool expected)
 {
  var id=Guid.NewGuid();var entity=Guid.NewGuid();
  var entries=new[]{new OutboxEntry(new(id,entity,kind,"{}",1,0,null),status,null),new OutboxEntry(new(Guid.NewGuid(),Guid.NewGuid(),"SubmitQCForm","{}",1,0,null),"Completed",null)};
  Assert.Equal(expected,RecordSyncState.OperationConfirmed(id,kind,entries));
 }
}
