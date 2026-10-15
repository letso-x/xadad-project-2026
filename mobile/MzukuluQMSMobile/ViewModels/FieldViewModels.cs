using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
namespace MzukuluQMSMobile.ViewModels;
public class FieldViewModel : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 private bool busy;private string message="";
 public bool Busy {get=>busy;private set{busy=value;Changed();}}
 public string Message {get=>message;set{message=value;Changed();}}
 protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
 public async Task<bool> RunAsync(Func<Task> action,string? success=null)
 {
  if(Busy)return false;Busy=true;
  try{await action();if(success is not null)Message=success;return true;}
  catch(Exception error) when(error is not OutOfMemoryException)
  {Message=error switch {FormatException=>"Check the number or date format. Your previous saved value is retained.",HttpRequestException=>"We couldn't connect to the service. Your saved work is still available.",TaskCanceledException=>"Connection timed out. Your saved work is still available.",InvalidOperationException or ArgumentException=>ApiFailureMessage.SafeDetail(error.Message)??"This action could not finish. Your saved work is retained.",_=>"This action could not finish. Your saved work is retained."};return false;}
  finally{Busy=false;}
 }
}
public sealed class FieldLoginViewModel(FieldSession session) : FieldViewModel
{
 public string Email {get;set;}="";public string Password {get;set;}="";
 public Task<bool> LoginAsync()=>RunAsync(async()=>{await session.LoginAsync(Email.Trim(),Password);Password="";Changed(nameof(Password));});
 public Task<bool> RestoreAsync()=>session.RestoreAsync();
}
public sealed class FieldListViewModel(InspectionWorkspace workspace) : FieldViewModel
{
 public JsonNode? Data {get;private set;}public bool Cached {get;private set;}
 public Task<bool> LoadAsync(Func<Task<JsonNode>> load)=>RunAsync(async()=>{Data=await load();Cached=workspace.IsCached;Message=Cached?"Showing saved information. Check pending sync.":"";Changed(nameof(Data));});
 public Task<bool> CreateClientAsync(JsonNode values)=>RunAsync(async()=>{await workspace.CreateClientAsync(values);},"Client created. Refresh the list.");
 public Task<bool> CreateProjectAsync(JsonNode values)=>RunAsync(async()=>{await workspace.CreateProjectAsync(values);},"Project created. Refresh the register.");
 public async Task<long?> CreateFormAsync(long project,JsonNode template)
 {long? id=null;await RunAsync(async()=>{id=await workspace.CreateFormAsync(project,template);Message=id.HasValue?"Form created":"Creation retained in sync queue. Inspect sync status before starting another copy.";});return id;}
}
public sealed class FieldFormViewModel(InspectionWorkspace workspace,long formId) : FieldViewModel
{
 public JsonNode? Form {get;private set;}public JsonArray Evidence {get;private set;}=[];public JsonArray SignOffs {get;private set;}=[];
 public JsonArray Ncrs {get;private set;}=[];public JsonArray Audit {get;private set;}=[];public string HistoryMessage {get;private set;}="";
 public bool LockedByQueue {get;private set;}
 public bool Editable=>!LockedByQueue&&Form is not null && FormInput.Editable(Form["status"]!.GetValue<string>())&&workspace.Permissions.Contains("PerformInspection");
 public bool Can(string permission)=>workspace.Permissions.Contains(permission);
 public async Task<bool> LoadAsync()=>await RunAsync(async()=>{
  Form=await workspace.FormAsync(formId);LockedByQueue=await (await workspace.StoreAsync()).HasPendingTransitionAsync(InspectionWorkspace.LocalId("form",formId));Message=workspace.IsCached?"Saved answers · pending sync":"";HistoryMessage="";
  foreach(var kind in new[]{"evidence","sign-offs","ncrs","audit"})
  {
   if(kind=="audit"&&!Can("ReadAudit") || kind=="ncrs"&&!Can("ReadNcr"))continue;
   try{var data=(await workspace.HistoryAsync(formId,kind)).AsArray();switch(kind){case "evidence":Evidence=data;break;case "sign-offs":SignOffs=data;break;case "ncrs":Ncrs=data;break;case "audit":Audit=data;break;}}
   catch(Exception){HistoryMessage+=" Some record history could not be loaded. Reconnect and try again.";}
  }
 });
 public Task<bool> SaveParsedAsync(JsonNode field,Func<JsonNode?> value,bool project)=>RunAsync(()=>workspace.SaveValueAsync(Form!,field,value(),project),"Pending sync");
 public Task<byte[]> EvidenceBytesAsync(Guid id)=>ReadEvidenceAsync(id);
 public Task<byte[]> UploadedEvidenceAsync(long evidenceId)=>workspace.UploadedEvidenceAsync(formId,evidenceId);
 public Task<bool> RemoveUnsentPhotoAsync(Guid evidenceId)=>RunAsync(async()=>{
  if(!Editable)throw new InvalidOperationException("This record is read-only.");
  await (await workspace.StoreAsync()).CancelUnsentPhotoAsync(InspectionWorkspace.LocalId("form",formId),evidenceId);
 },"Unsent photo removed. Uploaded evidence and saved answers are retained.");
 private async Task<byte[]> ReadEvidenceAsync(Guid id)=>await (await workspace.StoreAsync()).ReadEvidenceAsync(id);
 public async Task<Guid?> CaptureAsync(long fieldId,bool camera)
 {
  Guid? id=null;await RunAsync(async()=>{
   var file=await workspace.Media.PickAsync(camera);if(file is null){Message="Capture cancelled. Saved work retained.";return;}
   await using var stream=await file.OpenReadAsync();id=await workspace.SavePhotoAsync(Form!,fieldId,stream,file.ContentType??"image/jpeg",file.FileName);Message="Photo saved · pending upload";
  });return id;
 }
 public Task<bool> RemarksAsync(string text)=>RunAsync(()=>workspace.SaveRemarksAsync(Form!,text),"Remarks saved · pending sync");
 public async Task<IReadOnlyList<(Guid Id,long? Field,string Type,string Status)>> LocalEvidenceAsync()
 {
  var rows=new List<(Guid,long?,string,string)>();var local=await workspace.StoreAsync();
  foreach(var item in await local.OutboxAsync())
  {
   if(item.Operation.EntityId!=InspectionWorkspace.LocalId("form",formId)||item.Operation.Kind!="UploadEvidence")continue;
   var request=JsonSerializer.Deserialize<SyncRequest>(item.Operation.Payload)!;
   if(request.EvidenceId.HasValue)rows.Add((request.EvidenceId.Value,request.QCFormFieldID,request.EvidenceType??"Photo",item.Status));
  }
  return rows;
 }
 public Task<bool> SubmitAsync()=>RunAsync(async()=>{
  var photos=Evidence.Where(e=>e?["evidenceType"]?.GetValue<string>()=="Photo"&&e["qcFormFieldID"] is not null).Select(e=>e!["qcFormFieldID"]!.GetValue<long>()).ToHashSet();
  foreach(var item in await LocalEvidenceAsync())if(item.Type=="Photo"&&item.Field.HasValue&&item.Status!="Cancelled")photos.Add(item.Field.Value);
  var errors=FormInput.RequiredErrors(Form!,photos);if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
  await workspace.QueueSubmitAsync(Form!);Message=await workspace.SyncAsync();await LoadFormOnlyAsync();
 });
 private async Task LoadFormOnlyAsync()=>Form=await workspace.FormAsync(formId);
 public Task<bool> SyncAsync()=>RunAsync(async()=>{Message=await workspace.SyncAsync();await LoadFormOnlyAsync();});
 public Task<bool> ReviewAsync(bool approve,string? reason)=>RunAsync(async()=>{await workspace.QueueReviewAsync(Form!,approve,reason);Message=await workspace.SyncAsync();await LoadFormOnlyAsync();});
 public async Task<Guid?> PhotoAsync(long fieldId,Stream stream,string type,string name)
 {Guid? id=null;await RunAsync(async()=>{id=await workspace.SavePhotoAsync(Form!,fieldId,stream,type,name);},"Photo saved · pending upload");return id;}
 public async Task<bool> SignatureAsync(SignatureInk ink,long? fieldId,string type,string? comments,int width=600,int height=240)=>await RunAsync(async()=>{
  using var image=new MemoryStream(ink.ToPng(width,height));await workspace.SaveSignatureAsync(Form!,fieldId,image,type,comments);Message=await workspace.SyncAsync();await LoadFormOnlyAsync();
 });
}
