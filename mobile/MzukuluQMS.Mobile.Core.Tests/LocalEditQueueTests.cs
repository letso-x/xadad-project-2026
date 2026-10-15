using System.Text;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class LocalEditQueueTests
{
 private sealed class Keys:ILocalKeyStore{public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(new byte[32]);}
 [Fact]public async Task Rapid_captured_edits_finish_in_order_and_drain_waits_for_durable_write()
 {
  var queue=new LocalEditQueue();var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var saved=new List<string>();
  var first=queue.Enqueue(async()=>{await release.Task;saved.Add("A");});
  var second=queue.Enqueue(()=>{saved.Add("R");return Task.CompletedTask;});
  var third=queue.Enqueue(()=>{saved.Add("N/A");return Task.CompletedTask;});
  var drain=queue.DrainAsync();Assert.False(drain.IsCompleted);Assert.Empty(saved);
  release.SetResult();await Task.WhenAll(first,second,third,drain);Assert.Equal(new[]{"A","R","N/A"},saved);
 }
 [Fact]public async Task Write_failure_is_observable_and_does_not_discard_the_next_edit()
 {
  var queue=new LocalEditQueue();var saved=false;
  var failed=queue.Enqueue(()=>Task.FromException(new IOException("synthetic storage failure")));
  await Assert.ThrowsAsync<IOException>(()=>failed);
  await queue.Enqueue(()=>{saved=true;return Task.CompletedTask;});await queue.DrainAsync();Assert.True(saved);
 }
 [Fact]public async Task Incomplete_input_survives_reopen_without_mutating_existing_sync_intent()
 {
  var folder=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());var owner=Guid.NewGuid();var entity=Guid.NewGuid();var draft=Guid.NewGuid();Guid operation;
  await using(var store=new OfflineStore(folder,owner,new Keys()))
  {
   operation=await store.SaveAndEnqueueAsync(entity,"{\"value\":12}","UpdateQCResponse","retained request",4);
   await store.CacheAsync(draft,null,"{\"text\":\"unfinished-number-\",\"value\":null}");
  }
  await using(var store=new OfflineStore(folder,owner,new Keys()))
  {
   Assert.Equal("unfinished-number-",JsonNode.Parse((await store.GetAsync(draft))!.Json)!["text"]!.ToString());
   var existing=Assert.Single(await store.OutboxAsync());Assert.Equal(operation,existing.Operation.OperationId);Assert.Equal("retained request",existing.Operation.Payload);Assert.Equal(4,existing.Operation.ExpectedRevision);
   Assert.Equal(12,JsonNode.Parse((await store.GetAsync(entity))!.Json)!["value"]!.GetValue<int>());
   await store.CacheAsync(draft,null,"null");Assert.Null(JsonNode.Parse((await store.GetAsync(draft))!.Json));
  }
  Assert.DoesNotContain("unfinished-number-",Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(folder,$"qms-{owner:N}.db"))));
 }
}
