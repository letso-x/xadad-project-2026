using MzukuluQMS.Mobile.Core;
using System.Security.Cryptography;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class SyncEngineTests
{
 private sealed class Keys : ILocalKeyStore {public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(new byte[32]);}
 private sealed class Session(Guid? owner,string? token="token") : ISyncSession {public Guid? Owner=>owner;public Task<string?> GetAccessTokenAsync(CancellationToken ct)=>Task.FromResult(token);}
 private sealed class Transport(int status) : ISyncTransport {public int Calls;public Task<SyncReply> SendAsync(PendingOperation op,CancellationToken ct){Calls++;return Task.FromResult(new SyncReply(status,"response",5,null,"server-snapshot"));}}
 [Theory][InlineData(200,"Synced","Completed")][InlineData(403,"Sync failed","Failed")][InlineData(409,"Conflict","Conflict")][InlineData(500,"Pending sync","Pending")][InlineData(401,"Authentication required","Pending")]
 public async Task Statuses_preserve_or_acknowledge_operations_correctly(int code,string expected,string state)
 {
  var owner=Guid.NewGuid();await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),owner,new Keys());await store.SaveAndEnqueueAsync(Guid.NewGuid(),"local","UpdateQCResponse","payload",1);var transport=new Transport(code);
  Assert.Equal(expected,await new SyncEngine(store,new Session(owner),transport).RunAsync());Assert.Equal(1,(await store.CountsAsync())[state]);Assert.Equal(1,transport.Calls);
  if(code==409)Assert.Equal("server-snapshot",Assert.Single(await store.ConflictsAsync()).ServerJson);
 }
 [Fact]public async Task Another_account_never_transmits_prior_owners_queue()
 {
  var owner=Guid.NewGuid();await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),owner,new Keys());await store.SaveAndEnqueueAsync(Guid.NewGuid(),"local","UpdateQCResponse","payload",1);var transport=new Transport(200);
  Assert.Equal("Authentication required",await new SyncEngine(store,new Session(Guid.NewGuid()),transport).RunAsync());Assert.Equal(0,transport.Calls);Assert.Equal(1,(await store.CountsAsync())["Pending"]);
 }
}
