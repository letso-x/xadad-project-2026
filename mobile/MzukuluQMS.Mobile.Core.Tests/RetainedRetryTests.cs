using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;

public sealed class RetainedRetryTests
{
 sealed class Keys:ILocalKeyStore {public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(new byte[32]);}
 [Theory][InlineData(true)][InlineData(false)]
 public async Task Explicit_retry_preserves_fingerprint_and_history_and_unblocks_order_only_after_ack(bool transient)
 {
  await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),Guid.NewGuid(),new Keys());var entity=Guid.NewGuid();
  var first=await store.SaveAndEnqueueAsync(entity,"local","UpdateQCFormProjectField","original-bytes",7);
  var second=await store.SaveAndEnqueueAsync(entity,"local","SubmitQCForm","submit-bytes",7,first);
  var attempt=(await store.ClaimAsync())!;await store.FailAsync(attempt,"Service unavailable",transient,failureJson:"retained-error");
  Assert.Null(await store.ClaimAsync());await store.RetryRetainedAsync(first);
  var retried=(await store.ClaimAsync())!;Assert.Equal(first,retried.OperationId);Assert.Equal("original-bytes",retried.Payload);Assert.Equal(7,retried.ExpectedRevision);Assert.Equal(1,retried.Attempts);
  Assert.Equal("retained-error",(await store.OutboxAsync())[0].FailureJson);Assert.Null(await store.ClaimAsync());
  await store.CompleteAsync(retried,null,8);var next=(await store.ClaimAsync())!;Assert.Equal(second,next.OperationId);Assert.Equal(8,next.ExpectedRevision);
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Retry_never_replays_completed_intent_or_bypasses_conflict(bool conflict)
 {
  await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),Guid.NewGuid(),new Keys());
  var id=await store.SaveAndEnqueueAsync(Guid.NewGuid(),"local","UpdateQCResponse","bytes",1);var op=(await store.ClaimAsync())!;
  if(conflict)await store.FailAsync(op,"Central changed",false,true,"server-version");else await store.CompleteAsync(op,null,2);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.RetryRetainedAsync(id));Assert.Null(await store.ClaimAsync());
  if(conflict)Assert.Equal("server-version",Assert.Single(await store.ConflictsAsync()).ServerJson);
 }
}
