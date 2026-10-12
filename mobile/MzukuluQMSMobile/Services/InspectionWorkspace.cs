using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Constants;
namespace MzukuluQMSMobile.Services;
public sealed class InspectionWorkspace(FieldSession session) : IAsyncDisposable
{
 private OfflineStore? store;
 private readonly HttpClient client=new(){BaseAddress=new Uri(ApiConstants.BaseUrl),Timeout=TimeSpan.FromSeconds(30)};
 public async Task<OfflineStore> StoreAsync()
 {
  var owner=session.Owner??throw new InvalidOperationException("Login is required.");
  if(store is not null && store.Owner!=owner){await store.DisposeAsync();store=null;}
  store??=new OfflineStore(FileSystem.AppDataDirectory,owner,session);await store.InitialiseAsync();return store;
 }
 public static Guid LocalId(string kind,long id)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(kind+":"+id)).AsSpan(0,16));
 private async Task<JsonNode> GetAsync(string route)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,route);var token=await session.GetAccessTokenAsync(default);
  if(token is null)throw new InvalidOperationException("Session expired; local work is retained. Login online to resume sync.");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);using var response=await client.SendAsync(request);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("API returned HTTP "+(int)response.StatusCode);
  return JsonNode.Parse(await response.Content.ReadAsStringAsync())??throw new InvalidOperationException("API returned no data.");
 }
 public async Task<JsonNode> CachedGetAsync(string kind,long id,string route)
 {
  var local=await StoreAsync();var localId=LocalId(kind,id);
  try{var node=await GetAsync(route);await local.CacheAsync(localId,id,node.ToJsonString());}
  catch(HttpRequestException){if(await local.GetAsync(localId) is null)throw;}
  catch(TaskCanceledException){if(await local.GetAsync(localId) is null)throw;}
  return JsonNode.Parse((await local.GetAsync(localId))!.Json)!;
 }
 public Task<JsonNode> ProjectsAsync()=>CachedGetAsync("projects",0,"api/projects");
 public Task<JsonNode> FormsAsync(long projectId)=>CachedGetAsync("forms",projectId,$"api/qc-forms?projectId={projectId}&pageSize=100");
 public Task<JsonNode> FormAsync(long formId)=>CachedGetAsync("form",formId,$"api/qc-forms/{formId}");
 public async Task SaveValueAsync(JsonNode form,JsonNode field,JsonNode? value,bool projectField)
 {
  var status=form["status"]!.GetValue<string>();if(status is not ("Draft" or "InProgress" or "ReturnedForCorrection"))throw new InvalidOperationException("This record is read-only.");
  var id=form["qcFormID"]!.GetValue<long>();var target=field[projectField?"qcFormProjectFieldID":"qcResponseID"]!.GetValue<long>();
  var previous=field["value"]?.DeepClone();
  field["value"]=value?.DeepClone();var route=$"api/qc-forms/{id}/{(projectField?"project-fields":"responses")}/{target}";
  var request=new SyncRequest("PUT",route,new JsonObject { ["value"]=value?.DeepClone() }.ToJsonString());
  try {await (await StoreAsync()).SaveAndEnqueueAsync(LocalId("form",id),form.ToJsonString(),projectField?"UpdateQCFormProjectField":"UpdateQCResponse",JsonSerializer.Serialize(request),form["revision"]!.GetValue<long>());}
  catch {field["value"]=previous;throw;}
 }
 public async Task QueueSubmitAsync(JsonNode form)
 {
  if(form["status"]!.GetValue<string>() is not ("Draft" or "InProgress" or "ReturnedForCorrection"))throw new InvalidOperationException("This record cannot be submitted from its current status.");
  var id=form["qcFormID"]!.GetValue<long>();var request=new SyncRequest("POST",$"api/qc-forms/{id}/submit");
  await (await StoreAsync()).SaveAndEnqueueAsync(LocalId("form",id),form.ToJsonString(),"SubmitQCForm",JsonSerializer.Serialize(request),form["revision"]!.GetValue<long>());
 }
 public async Task<string> SyncAsync()
 {
  var local=await StoreAsync();return await new SyncEngine(local,session,new HttpSyncTransport(client,local,session)).RunAsync();
 }
 public async ValueTask DisposeAsync(){if(store is not null)await store.DisposeAsync();store=null;}
}
