using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
namespace MzukuluQMSMobile.ViewModels;

public sealed class NcrViewModel(InspectionWorkspace workspace,long formId,long ncrId,long? publicId):FieldViewModel
{
 private readonly SemaphoreSlim drafts=new(1,1);
 public JsonNode? Form {get;private set;}
 public JsonNode? Ncr {get;private set;}
 public JsonNode Correction {get;private set;}=new JsonObject();
 public JsonArray Evidence {get;private set;}=[];
 private string DraftKey=>"ncr-correction:"+ncrId;
 public bool Can(string permission)=>workspace.Permissions.Contains(permission);
 public async Task<bool> LoadAsync()=>await RunAsync(async()=>{
  Form=await workspace.FormAsync(formId);Ncr=await workspace.NcrAsync(formId,ncrId,publicId);
  Evidence=(await workspace.HistoryAsync(formId,"evidence")).AsArray();
  Correction=await workspace.InputDraftAsync(formId,DraftKey)??FreshCorrection();
 });
 private JsonNode FreshCorrection()=>new JsonObject{
   ["severity"]=Ncr!["severity"]?.DeepClone(),["classification"]=Ncr!["classification"]?.DeepClone(),
   ["isBlocking"]=Ncr!["isBlocking"]?.DeepClone(),["disposition"]=Ncr!["disposition"]?.DeepClone(),
   ["correctiveAction"]=Ncr!["correctiveAction"]?.DeepClone(),["correctiveEvidenceID"]=Ncr!["correctiveEvidenceID"]?.DeepClone(),
   ["expectedRevision"]=Ncr!["revision"]?.DeepClone()};
 public async Task PersistDraftAsync()
 {await drafts.WaitAsync();try{await workspace.SaveInputDraftAsync(formId,DraftKey,Correction.DeepClone());}finally{drafts.Release();}}
 public async Task RetainDraftAsync()
 {try{await PersistDraftAsync();}catch(Exception e) when(e is not OutOfMemoryException){Message="This NCR draft could not be saved. Keep the page open and try again.";}}
 public Task<bool> CorrectAsync()=>RunAsync(async()=>{
  await PersistDraftAsync();await workspace.QueueNcrCorrectionAsync(Form!,Ncr!,Correction);Message=await workspace.SyncAsync();
  if(Message=="Synced"){await workspace.SaveInputDraftAsync(formId,DraftKey,null);await ReloadAsync();Correction=FreshCorrection();Message="Corrective action saved. Independent verification is required.";}
 });
 public Task<bool> VerifyAsync(bool passed,string comments)=>RunAsync(async()=>{
  await workspace.QueueNcrVerificationAsync(Form!,Ncr!,passed,comments);Message=await workspace.SyncAsync();
  if(Message=="Synced"){await ReloadAsync();Message=passed?"Verification passed — NCR closed.":"Verification failed — NCR remains open.";}
 });
 public Task<bool> SyncAsync()=>RunAsync(async()=>{Message=await workspace.SyncAsync();await ReloadAsync();});
 private async Task ReloadAsync(){Form=await workspace.FormAsync(formId);Ncr=await workspace.NcrAsync(formId,ncrId,publicId);Evidence=(await workspace.HistoryAsync(formId,"evidence")).AsArray();}
 public async Task<bool> CaptureEvidenceAsync(bool camera)=>await RunAsync(async()=>{
  var file=await workspace.Media.PickAsync(camera);if(file is null){Message="Photo selection cancelled. Saved work retained.";return;}
  await using var stream=await file.OpenReadAsync();await workspace.SavePhotoAsync(Form!,null,stream,file.ContentType??"image/jpeg",file.FileName);
  Message=await workspace.SyncAsync();await ReloadAsync();
  if(Message=="Synced")Message="Photo uploaded. Select it as corrective evidence before saving the action.";
 });
 public Task<byte[]> PreviewAsync(long evidenceId)=>workspace.UploadedEvidenceAsync(formId,evidenceId);
}
