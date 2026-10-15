using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;
public sealed partial class OfflineStore
{
 public async Task<byte[]?> ReadAcknowledgedPhotoAsync(long serverFormId,long evidenceId,CancellationToken ct=default)
 {
  Guid? localId=null;
  await Locked(async()=>{
   await using var cmd=Command("SELECT o.Payload,r.Payload FROM Operation o JOIN OperationResult r ON r.OperationId=o.OperationId JOIN Entity e ON e.LocalId=o.EntityId WHERE o.Kind='UploadEvidence' AND o.Status='Completed' AND e.ServerId=$form",null,("$form",serverFormId));
   await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))
   {
    var request=JsonSerializer.Deserialize<SyncRequest>(Decrypt((byte[])reader[0]));
    if(request?.EvidenceType!="Photo")continue;
    var result=JsonNode.Parse(Decrypt((byte[])reader[1]));
    if(result?["evidenceID"]?.ToString()==evidenceId.ToString(System.Globalization.CultureInfo.InvariantCulture)){localId=request.EvidenceId;break;}
   }
  },ct);
  return localId.HasValue?await ReadEvidenceAsync(localId.Value,ct):null;
 }
 public async Task CancelUnsentPhotoAsync(Guid entityId,Guid evidenceId,CancellationToken ct=default)
 {
  await Locked(async()=>{
   using var tx=connection!.BeginTransaction();
   await using var entityCommand=Command("SELECT Payload FROM Entity WHERE LocalId=$entity",tx,("$entity",entityId.ToString()));
   var bytes=await entityCommand.ExecuteScalarAsync(ct) as byte[]??throw new KeyNotFoundException("Saved record was not found.");
   var form=JsonNode.Parse(Decrypt(bytes))!;
   if(!FormInput.Editable(form["status"]!.ToString()))throw new InvalidOperationException("Evidence on this record is protected.");
   await using var lockCommand=Command("SELECT EXISTS(SELECT 1 FROM Operation WHERE EntityId=$entity AND Status NOT IN('Completed','Cancelled') AND Kind IN('SubmitQCForm','ReturnForCorrection','ApproveQCForm','CreateSignOff'))",tx,("$entity",entityId.ToString()));
   if(Convert.ToInt32(await lockCommand.ExecuteScalarAsync(ct))!=0)throw new InvalidOperationException("A workflow action is pending. Finish it before managing photos.");
   var candidates=new List<(Guid Id,SyncRequest Request)>();
   await using(var cmd=Command("SELECT OperationId,Payload FROM Operation WHERE EntityId=$entity AND Kind='UploadEvidence' AND Status='Pending' AND Attempts=0 AND NOT EXISTS(SELECT 1 FROM Operation child WHERE child.DependsOn=Operation.OperationId)",tx,("$entity",entityId.ToString())))
   await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))candidates.Add((Guid.Parse(reader.GetString(0)),JsonSerializer.Deserialize<SyncRequest>(Decrypt((byte[])reader[1]))!));
   var match=candidates.SingleOrDefault(c=>c.Request.EvidenceId==evidenceId&&c.Request.EvidenceType=="Photo");
   if(match.Id==Guid.Empty)throw new InvalidOperationException("Only a photo that has never been sent can be removed. Uploaded and attempted evidence is retained.");
   await ExecuteAsync("INSERT INTO OperationDecision VALUES($id,$reason,$now); UPDATE Operation SET Status='Cancelled' WHERE OperationId=$id; UPDATE Entity SET Dirty=EXISTS(SELECT 1 FROM Operation WHERE EntityId=$entity AND Status NOT IN('Completed','Cancelled')) WHERE LocalId=$entity",tx,ct,
    ("$id",match.Id.ToString()),("$reason",Encrypt("User removed an unsent photo before submission. File retained for recovery.")),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),("$entity",entityId.ToString()));
   tx.Commit();
  },ct);
 }
}
