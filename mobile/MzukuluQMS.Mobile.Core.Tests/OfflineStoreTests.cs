using System.Security.Cryptography;
using System.Text;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class OfflineStoreTests
{
 private sealed class Keys : ILocalKeyStore {private readonly byte[] key=RandomNumberGenerator.GetBytes(32);public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(key.ToArray());}
 private readonly string directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));
 [Fact] public async Task Dirty_values_outbox_and_interrupted_processing_survive_reopen()
 {
  var owner=Guid.NewGuid();var entity=Guid.NewGuid();var keys=new Keys();Guid operation;
  await using(var store=new OfflineStore(directory,owner,keys))
  {await store.CacheAsync(entity,71,"server");operation=await store.SaveAndEnqueueAsync(entity,"offline-value","UpdateQCResponse","payload",4);Assert.Equal(operation,(await store.ClaimAsync())!.OperationId);}
  await using(var store=new OfflineStore(directory,owner,keys))
  {Assert.Equal("offline-value",(await store.GetAsync(entity))!.Json);Assert.Equal(operation,(await store.ClaimAsync())!.OperationId);await store.CacheAsync(entity,71,"new-server-value");Assert.Equal("offline-value",(await store.GetAsync(entity))!.Json);}
 }
 [Fact] public async Task Completed_operations_stop_retrying_and_dependencies_preserve_order()
 {
  await using var store=new OfflineStore(directory,Guid.NewGuid(),new Keys());var entity=Guid.NewGuid();
  var first=await store.SaveAndEnqueueAsync(entity,"local","CreateQCForm","one",null);
  var second=await store.SaveAndEnqueueAsync(entity,"local","SubmitQCForm","two",0,first);
  var claimed=(await store.ClaimAsync())!;Assert.Equal(first,claimed.OperationId);Assert.Null(await store.ClaimAsync());
  await store.CompleteAsync(claimed,71,2);var next=(await store.ClaimAsync())!;Assert.Equal(second,next.OperationId);Assert.Equal(2,next.ExpectedRevision);
  await store.CompleteAsync(next,null,3);Assert.Null(await store.ClaimAsync());Assert.False((await store.GetAsync(entity))!.Dirty);
 }
 [Fact] public async Task Conflicts_preserve_both_versions_across_restart()
 {
  var owner=Guid.NewGuid();var keys=new Keys();var entity=Guid.NewGuid();
  await using(var store=new OfflineStore(directory,owner,keys)){await store.SaveAndEnqueueAsync(entity,"local","UpdateQCResponse","local-request",1);var op=(await store.ClaimAsync())!;await store.FailAsync(op,"Changed",false,true,"server-response");}
  await using(var store=new OfflineStore(directory,owner,keys)){var conflict=Assert.Single(await store.ConflictsAsync());Assert.Equal("local-request",conflict.LocalJson);Assert.Equal("server-response",conflict.ServerJson);Assert.Equal("local",(await store.GetAsync(entity))!.Json);Assert.Null(await store.ClaimAsync());}
 }
 [Fact] public async Task Owners_cannot_read_each_others_data()
 {
  var keys=new Keys();var entity=Guid.NewGuid();await using var first=new OfflineStore(directory,Guid.NewGuid(),keys);await first.CacheAsync(entity,71,"protected");
  await using var second=new OfflineStore(directory,Guid.NewGuid(),keys);Assert.Null(await second.GetAsync(entity));
 }
 [Fact] public async Task Evidence_survives_reopen_and_is_encrypted_on_disk()
 {
  var keys=new Keys();var owner=Guid.NewGuid();var entity=Guid.NewGuid();Guid evidence;var bytes=Encoding.UTF8.GetBytes("synthetic evidence");
  await using(var store=new OfflineStore(directory,owner,keys)){await store.CacheAsync(entity,71,"snapshot");using var stream=new MemoryStream(bytes);evidence=await store.SaveEvidenceAsync(entity,stream,"image/png","photo.png");}
  await using(var store=new OfflineStore(directory,owner,keys)){Assert.Equal(bytes,await store.ReadEvidenceAsync(evidence));}
  Assert.NotEqual(bytes,await File.ReadAllBytesAsync(Directory.GetFiles(Path.Combine(directory,$"evidence-{owner:N}"))[0]));
 }
 [Fact] public async Task Only_never_attempted_photo_can_be_cancelled_and_file_is_retained()
 {
  await using var store=new OfflineStore(directory,Guid.NewGuid(),new Keys());var entity=Guid.NewGuid();const string form="{\"qcFormID\":10,\"status\":\"Draft\",\"revision\":0}";
  await store.CacheAsync(entity,10,form);using var image=new MemoryStream(new byte[]{137,80,78,71});
  var first=await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/10/evidence",EvidenceId:id,EvidenceType:"Photo"),localJson:form,expectedRevision:0);
  await store.CancelUnsentPhotoAsync(entity,first);Assert.Equal("Cancelled",Assert.Single(await store.OutboxAsync()).Status);Assert.Equal(new byte[]{137,80,78,71},await store.ReadEvidenceAsync(first));Assert.Null(await store.ClaimAsync());
  image.Position=0;var second=await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/10/evidence",EvidenceId:id,EvidenceType:"Photo"),localJson:form,expectedRevision:0);
  var attempted=await store.ClaimAsync();await store.FailAsync(attempted!,"Network interrupted",true);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.CancelUnsentPhotoAsync(entity,second));Assert.Equal(new byte[]{137,80,78,71},await store.ReadEvidenceAsync(second));
 }
 [Fact] public async Task Signature_or_pending_submission_protects_evidence_from_removal()
 {
  await using var store=new OfflineStore(directory,Guid.NewGuid(),new Keys());var entity=Guid.NewGuid();const string form="{\"qcFormID\":10,\"status\":\"Draft\",\"revision\":0}";
  await store.CacheAsync(entity,10,form);using var image=new MemoryStream(new byte[]{137,80,78,71});
  var photo=await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/10/evidence",EvidenceId:id,EvidenceType:"Signature"),localJson:form,expectedRevision:0);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.CancelUnsentPhotoAsync(entity,photo));
  image.Position=0;var unsent=await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/10/evidence",EvidenceId:id,EvidenceType:"Photo"),localJson:form,expectedRevision:0);
  await store.SaveAndEnqueueAsync(entity,form,"SubmitQCForm","{}",0);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.CancelUnsentPhotoAsync(entity,unsent));
 }
 [Fact] public async Task Acknowledged_photo_preview_survives_restart_but_cannot_cross_forms()
 {
  var owner=Guid.NewGuid();var keys=new Keys();var entity=Guid.NewGuid();var bytes=new byte[]{137,80,78,71};
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   await store.CacheAsync(entity,71,"{\"qcFormID\":71,\"status\":\"Draft\",\"revision\":0}");using var image=new MemoryStream(bytes);
   await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/71/evidence",EvidenceId:id,EvidenceType:"Photo"),localJson:"{\"qcFormID\":71,\"status\":\"Draft\",\"revision\":0}",expectedRevision:0);
   var upload=await store.ClaimAsync();await store.CompleteAsync(upload!,null,1,resultJson:"{\"evidenceID\":9}");
  }
  await using(var store=new OfflineStore(directory,owner,keys)){Assert.Equal(bytes,await store.ReadAcknowledgedPhotoAsync(71,9));Assert.Null(await store.ReadAcknowledgedPhotoAsync(72,9));Assert.Null(await store.ReadAcknowledgedPhotoAsync(71,99));}
 }
}
