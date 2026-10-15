using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Constants;
namespace MzukuluQMSMobile.Services;
public sealed partial class InspectionWorkspace(FieldSession session,IFieldMedia media) : IAsyncDisposable
{
 private sealed class CachedDataUnavailableException(string message):InvalidOperationException(message);
 public IFieldMedia Media=>media;
 private OfflineStore? store;
 private readonly SemaphoreSlim syncGate=new(1,1);
 private readonly HttpClient client=new(){BaseAddress=new Uri(ApiConstants.BaseUrl),Timeout=TimeSpan.FromSeconds(30)};
 public bool IsCached {get;private set;}
 public HashSet<string> Permissions {get;private set;}=new(StringComparer.Ordinal);
 public async Task<OfflineStore> StoreAsync()
 {
  var owner=session.Owner??throw new InvalidOperationException("Login is required.");
  if(store is not null && store.Owner!=owner){await store.DisposeAsync();store=null;Permissions.Clear();}
  store??=new OfflineStore(FileSystem.AppDataDirectory,owner,session);await store.InitialiseAsync();return store;
 }
 public static Guid LocalId(string kind,long id)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(kind+":"+id)).AsSpan(0,16));
 private async Task<JsonNode> RequestAsync(string method,string route,JsonNode? body=null)
 {
  using var request=new HttpRequestMessage(new HttpMethod(method),route);
  var token=await session.GetAccessTokenAsync(default);
  if(token is null)throw new InvalidOperationException("Session expired. Local work is retained; sign in online to sync.");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(body is not null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
  using var response=await client.SendAsync(request);
  if(!response.IsSuccessStatusCode)
  {
   var message=ApiFailureMessage.Describe(response.StatusCode,await response.Content.ReadAsStringAsync());
   if((int)response.StatusCode>=500||response.StatusCode==HttpStatusCode.TooManyRequests)throw new HttpRequestException(message,null,response.StatusCode);
   throw new InvalidOperationException(message);
  }
  var text=await response.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(text)?new JsonObject():JsonNode.Parse(text)!;
 }
 public async Task<JsonNode> CachedGetAsync(string kind,long id,string route)
 {
  var local=await StoreAsync();var key=LocalId(kind,id);IsCached=false;
  try{var node=await RequestAsync("GET",route);await local.CacheAsync(key,id,node.ToJsonString());}
  catch(Exception e) when(e is HttpRequestException or TaskCanceledException)
  {if(await local.GetAsync(key) is null)throw new CachedDataUnavailableException("We couldn't connect to the service, and this information is not yet saved on this device.");IsCached=true;}
  var cached=(await local.GetAsync(key))!;IsCached|=cached.Dirty;return JsonNode.Parse(cached.Json)!;
 }
 public async Task LoadPermissionsAsync()
 {
  var node=await CachedGetAsync("permissions",0,"api/auth/permissions");
  Permissions=node["permissions"]!.AsArray().Select(p=>p!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
 }
 public Task<JsonNode> DashboardAsync()=>CachedGetAsync("dashboard",0,"api/quality/dashboard");
 public Task<JsonNode> NotificationsAsync()=>CachedGetAsync("notifications",0,"api/quality/notifications");
 public Task<JsonNode> ReadNotificationAsync(long id)=>RequestAsync("POST",$"api/quality/notifications/{id}/read");
 public Task<JsonNode> ProjectsAsync()=>CachedGetAsync("projects",0,"api/projects");
 public Task<JsonNode> ClientsAsync()=>CachedGetAsync("clients",0,"api/clients");
 public Task<JsonNode> TemplatesAsync()=>CachedGetAsync("templates",0,"api/checklist-templates");
 public Task<JsonNode> TemplateAsync(long version)=>CachedGetAsync("template",version,$"api/checklist-template-versions/{version}");
 public async Task<JsonNode> FormsAsync(long projectId)
 {
  var local=await StoreAsync();var pending=new List<(JsonNode Form,long? Server)>();
  foreach(var record in await local.RecordsAsync())
  {
   var node=JsonNode.Parse(record.Json);if(node is JsonObject form&&long.TryParse(form["qcFormID"]?.ToString(),out var id)&&id<0&&form["projectID"]?.ToString()==projectId.ToString()&&(record.Dirty||record.ServerId is null))pending.Add((form,record.ServerId));
  }
  JsonArray remote;try{remote=(await CachedGetAsync("forms",projectId,$"api/qc-forms?projectId={projectId}&pageSize=100")).AsArray();}
  catch(CachedDataUnavailableException) when(pending.Count>0){remote=new JsonArray();IsCached=true;}
  var result=new JsonArray();foreach(var item in pending)result.Add(item.Form.DeepClone());
  foreach(var form in remote)if(!pending.Any(p=>p.Server?.ToString()==form?["qcFormID"]?.ToString()))result.Add(form?.DeepClone());return result;
 }
 public async Task<JsonNode> FormAsync(long formId)
 {
  if(formId>0)return await CachedGetAsync("form",formId,$"api/qc-forms/{formId}");
  var local=await StoreAsync();var record=await local.GetAsync(LocalId("form",formId))??throw new InvalidOperationException("This saved record is unavailable for this account.");
  var form=JsonNode.Parse(record.Json)!;IsCached=true;if(record.ServerId is not long serverId||record.Dirty)return form;
  try{form=OfflineFormIdentity.Localize(await RequestAsync("GET",$"api/qc-forms/{serverId}"),form);await local.CacheAsync(record.LocalId,serverId,form.ToJsonString());IsCached=false;}
  catch(Exception e) when(e is HttpRequestException or TaskCanceledException){ }
  return form;
 }
 private async Task<long?> ServerFormIdAsync(long id)=>id>0?id:(await (await StoreAsync()).GetAsync(LocalId("form",id)))?.ServerId;
 public async Task<JsonNode> HistoryAsync(long formId,string kind)
 {
  var serverId=await ServerFormIdAsync(formId);if(serverId is null)return new JsonArray();
  var data=await CachedGetAsync(kind,formId,$"api/qc-forms/{serverId}/{(kind=="audit"?"audit-history":kind)}");
  if(formId<0){var form=await FormAsync(formId);if(form["serverIdMap"] is not null)data=OfflineFormIdentity.Localize(data,form);}
  return data;
 }
 public Task<JsonNode> CreateTemplateAsync(JsonNode values)=>RequestAsync("POST",values["revisionTemplateID"] is null?"api/checklist-templates":$"api/checklist-templates/{values["revisionTemplateID"]}/revisions?expectedVersionId={values["expectedVersionID"]}",values);
 public Task<JsonNode> CreateClientAsync(JsonNode values)=>RequestAsync("POST","api/clients",values);
 public Task<JsonNode> CreateProjectAsync(JsonNode values)=>RequestAsync("POST","api/projects",values);
 public async Task<long?> CreateFormAsync(long projectId,JsonNode template)
 {
  if(!Permissions.Contains("PerformInspection"))throw new InvalidOperationException("You do not have permission to start an inspection.");
  var version=template["activeVersionID"]?.GetValue<long>()??throw new InvalidOperationException("No active version is available.");
  var definition=await TemplateAsync(version);var projects=(await ProjectsAsync()).AsArray();
  var project=projects.FirstOrDefault(p=>p?["projectID"]?.GetValue<long>()==projectId)??throw new InvalidOperationException("This project is no longer accessible. Refresh projects before continuing.");
  var local=await StoreAsync();long id;do{id=-Random.Shared.NextInt64(1000000,long.MaxValue);}while(await local.GetAsync(LocalId("form",id)) is not null);
  var snapshot=OfflineFormIdentity.Create(id,projectId,definition,project,session.Owner!.Value);
  var body=new JsonObject{["projectID"]=projectId,["checklistTemplateID"]=template["checklistTemplateID"]!.DeepClone(),["checklistTemplateVersionID"]=version};
  await local.SaveAndEnqueueAsync(LocalId("form",id),snapshot.ToJsonString(),"CreateQCForm",JsonSerializer.Serialize(new SyncRequest("POST","api/qc-forms",body.ToJsonString())),null);
  await SyncAsync();return id;
 }
 private async Task RequireEditableAsync(JsonNode form)
 {
  if(form["status"]!.GetValue<string>() is not ("Draft" or "InProgress" or "ReturnedForCorrection"))throw new InvalidOperationException("This record is read-only.");
  var local=await StoreAsync();var saved=await local.GetAsync(LocalId("form",form["qcFormID"]!.GetValue<long>()));
  var acknowledgedStatus=saved is null?null:JsonNode.Parse(saved.Json)?["status"]?.ToString();
  if(acknowledgedStatus is not null&&!FormInput.Editable(acknowledgedStatus))throw new InvalidOperationException("This record has become read-only. Your entered draft is retained; refresh before continuing.");
  if(await local.HasPendingTransitionAsync(LocalId("form",form["qcFormID"]!.GetValue<long>())))
   throw new InvalidOperationException("A workflow action is awaiting sync or review. Values remain retained; finish that action before editing.");
 }
 public async Task SaveValueAsync(JsonNode form,JsonNode field,JsonNode? value,bool projectField)
 {
  await RequireEditableAsync(form);FormInput.Validate(field,value);
  if(JsonNode.DeepEquals(field["value"],value))return;
  var id=form["qcFormID"]!.GetValue<long>();var target=field[projectField?"qcFormProjectFieldID":"qcResponseID"]!.GetValue<long>();
  var previous=field["value"]?.DeepClone();field["value"]=value?.DeepClone();
  try{await QueueAsync(form,projectField?"UpdateQCFormProjectField":"UpdateQCResponse",new SyncRequest("PUT",$"api/qc-forms/{id}/{(projectField?"project-fields":"responses")}/{target}",new JsonObject{["value"]=value?.DeepClone()}.ToJsonString()));}
  catch{field["value"]=previous;throw;}
 }
 private async Task QueueAsync(JsonNode form,string kind,SyncRequest request)
 {
  var id=form["qcFormID"]!.GetValue<long>();await (await StoreAsync()).SaveAndEnqueueAsync(LocalId("form",id),form.ToJsonString(),kind,JsonSerializer.Serialize(request),form["revision"]!.GetValue<long>());
 }
 // Incomplete input uses separate encrypted entries, never invalid server writes.
 private static Guid InputDraftId(long formId,string fieldKey)=>LocalId("input-draft:"+fieldKey,formId);
 public async Task<JsonNode?> InputDraftAsync(long formId,string fieldKey)
 {
  var record=await (await StoreAsync()).GetAsync(InputDraftId(formId,fieldKey));
  return record is null?null:JsonNode.Parse(record.Json);
 }
 public async Task SaveInputDraftAsync(long formId,string fieldKey,JsonNode? draft)
  =>await (await StoreAsync()).CacheAsync(InputDraftId(formId,fieldKey),null,draft?.ToJsonString()??"null");
 public async Task SaveRemarksAsync(JsonNode form,string remarks)
 {
  await RequireEditableAsync(form);var prior=form["remarks"]?.DeepClone();form["remarks"]=remarks;
  try{await QueueAsync(form,"UpdateRemarks",new SyncRequest("PUT",$"api/qc-forms/{form["qcFormID"]}/remarks",new JsonObject{["remarks"]=remarks}.ToJsonString()));}catch{form["remarks"]=prior;throw;}
 }
 public async Task QueueSubmitAsync(JsonNode form)
 {await RequireEditableAsync(form);await QueueAsync(form,"SubmitQCForm",new SyncRequest("POST",$"api/qc-forms/{form["qcFormID"]}/submit"));}
 public async Task QueueReviewAsync(JsonNode form,bool approve,string? reason)
 {
  if(form["status"]!.GetValue<string>()!="Submitted")throw new InvalidOperationException("Review requires a Submitted record.");
  if(!approve && string.IsNullOrWhiteSpace(reason))throw new ArgumentException("A correction reason is required.");
  var local=await StoreAsync();if((await local.GetAsync(LocalId("form",form["qcFormID"]!.GetValue<long>())))!.Dirty)throw new InvalidOperationException("Sync all pending work before review.");
  await QueueAsync(form,approve?"ApproveQCForm":"ReturnForCorrection",new SyncRequest("POST",$"api/qc-forms/{form["qcFormID"]}/{(approve?"approve":"return-for-correction")}",approve?null:new JsonObject{["reason"]=reason}.ToJsonString()));
 }
 public async Task<Guid> SavePhotoAsync(JsonNode form,long? fieldId,Stream image,string contentType,string fileName)
 {
  await RequireEditableAsync(form);var id=form["qcFormID"]!.GetValue<long>();
  return await (await StoreAsync()).SaveEvidenceAsync(LocalId("form",id),image,contentType,fileName,uploadRequest:evidence=>new SyncRequest("POST",$"api/qc-forms/{id}/evidence",EvidenceId:evidence,EvidenceType:"Photo",QCFormFieldID:fieldId),localJson:form.ToJsonString(),expectedRevision:form["revision"]!.GetValue<long>());
 }
 public async Task<Guid> SaveSignatureAsync(JsonNode form,long? fieldId,Stream image,string type,string? comments)
 {
  if(form["status"]!.GetValue<string>()!="Submitted")throw new InvalidOperationException("Submit and sync before providing fresh signing consent.");
  var id=form["qcFormID"]!.GetValue<long>();var local=await StoreAsync();if((await local.GetAsync(LocalId("form",id)))!.Dirty)throw new InvalidOperationException("Sync pending work before signing.");
  return await local.SaveEvidenceAsync(LocalId("form",id),image,"image/png","signature.png",uploadRequest:evidence=>new SyncRequest("POST",$"api/qc-forms/{id}/evidence",EvidenceId:evidence,EvidenceType:"Signature",QCFormFieldID:fieldId),localJson:form.ToJsonString(),expectedRevision:form["revision"]!.GetValue<long>(),signOffRequest:new SyncRequest("POST",$"api/qc-forms/{id}/sign-offs",new JsonObject{["signOffType"]=type,["comments"]=comments}.ToJsonString()));
 }
 public async Task<string> SyncAsync()
 {
  if(!await syncGate.WaitAsync(0))return "Syncing";
  try{var local=await StoreAsync();return await new SyncEngine(local,session,new HttpSyncTransport(client,local,session)).RunAsync();}
  finally{syncGate.Release();}
 }
 public async ValueTask DisposeAsync(){if(store is not null)await store.DisposeAsync();store=null;Permissions.Clear();}
}
