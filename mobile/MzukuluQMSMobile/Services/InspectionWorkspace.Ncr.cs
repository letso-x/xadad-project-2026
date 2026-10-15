using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Services;

public sealed partial class InspectionWorkspace
{
 public Guid? CurrentActor=>session.Owner;
 public async Task<JsonNode> NcrAsync(long formId,long ncrId,long? publicId)
 {
  var cases=(await HistoryAsync(formId,"ncrs")).AsArray();
  return cases.FirstOrDefault(n=>n?["ncrID"]?.GetValue<long>()==ncrId&&
    (ncrId>0||n?["publicNCRID"]?.GetValue<long>()==publicId))?.DeepClone()
    ??throw new InvalidOperationException("This NCR could not be loaded. Refresh the QC record and try again.");
 }
 private async Task RequireCleanNcrFormAsync(JsonNode form)
 {
  if(!NcrInteraction.CanChangeForForm(form["status"]?.ToString()))throw new InvalidOperationException("This QC record is protected. NCR actions cannot change an approved or cancelled record.");
  if((await (await StoreAsync()).GetAsync(LocalId("form",form["qcFormID"]!.GetValue<long>())))?.Dirty==true)
   throw new InvalidOperationException("Sync pending QC changes before taking another NCR action. Your saved work is retained.");
 }
 public async Task QueueNcrCorrectionAsync(JsonNode form,JsonNode ncr,JsonNode draft)
 {
  if(!Permissions.Contains("CorrectNcr"))throw new InvalidOperationException("You don't have permission to correct this NCR.");
  NcrInteraction.ValidateCorrection(ncr,draft);await RequireCleanNcrFormAsync(form);
  await QueueAsync(form,"CorrectNcr",new SyncRequest("POST",$"api/qc-forms/{form["qcFormID"]}/ncrs/{ncr["ncrID"]}/correct",draft.ToJsonString()));
 }
 public async Task QueueNcrVerificationAsync(JsonNode form,JsonNode ncr,bool passed,string comments)
 {
  if(!Permissions.Contains("VerifyNcr"))throw new InvalidOperationException("You don't have permission to verify this NCR.");
  NcrInteraction.ValidateVerification(ncr,session.Owner??throw new InvalidOperationException("Sign in again to continue."),comments);
  await RequireCleanNcrFormAsync(form);
  await QueueAsync(form,"VerifyNcr",new SyncRequest("POST",$"api/qc-forms/{form["qcFormID"]}/ncrs/{ncr["ncrID"]}/verify",
    new JsonObject{["passed"]=passed,["comments"]=comments,["expectedRevision"]=ncr["revision"]!.DeepClone()}.ToJsonString()));
 }
 public async Task<byte[]> UploadedEvidenceAsync(long formId,long evidenceId)
 {
  var serverId=await ServerFormIdAsync(formId)??throw new InvalidOperationException("Sync this record before loading an uploaded attachment.");
  var cached=await (await StoreAsync()).ReadAcknowledgedPhotoAsync(serverId,evidenceId);if(cached is not null)return cached;
  using var request=new HttpRequestMessage(HttpMethod.Get,$"api/qc-forms/{serverId}/evidence/{evidenceId}/content");
  var token=await session.GetAccessTokenAsync(default)??throw new InvalidOperationException("Sign in again to view this attachment.");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("This attachment could not be loaded. Reconnect and try again.");
  const int maximum=20*1024*1024;
  if(response.Content.Headers.ContentLength>maximum)throw new InvalidOperationException("This image is too large to preview.");
  await using var input=await response.Content.ReadAsStreamAsync();using var output=new MemoryStream();var buffer=new byte[81920];int count;
  while((count=await input.ReadAsync(buffer))>0){if(output.Length+count>maximum)throw new InvalidOperationException("This image is too large to preview.");await output.WriteAsync(buffer.AsMemory(0,count));}
  return output.ToArray();
 }
}
