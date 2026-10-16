using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Services;
public sealed partial class InspectionWorkspace
{
 public async Task<JsonNode> ItpsAsync(long project)
 {
  var data=await CachedGetAsync("itp-list",project,$"api/projects/{project}/itps");var cached=IsCached;
  if(!cached)foreach(var item in data.AsArray().Where(x=>x?["isCurrent"]?.GetValue<bool>()==true))
   try{await ItpAsync(project,item!["id"]!.GetValue<long>());cached|=IsCached;}catch(Exception e) when(e is HttpRequestException or TaskCanceledException){cached=true;}
  IsCached=cached;return data;
 }
 public async Task<JsonNode> ItpAsync(long project,long version)
 {
  try
  {
   var node=await CachedGetAsync("itp:"+project,version,$"api/projects/{project}/itps/{version}");
   if(!IsCached&&node["version"]?["status"]?.ToString()=="Approved")await ItpCache.SaveAsync(await StoreAsync(),project,version,node);
   return node;
  }
  catch(HttpRequestException){var cached=await ItpCache.ReadAsync(await StoreAsync(),project,version);if(cached is null)throw;IsCached=true;return cached;}
 }
 public Task<JsonNode> SaveItpAsync(long project,long? version,JsonNode definition,long stamp)=>RequestAsync(version.HasValue?"PUT":"POST",$"api/projects/{project}/itps"+(version.HasValue?$"/{version}":""),version.HasValue?new JsonObject{["expectedStamp"]=stamp,["definition"]=definition.DeepClone()}:definition);
 public Task<JsonNode> ActItpAsync(long project,long version,string action,long stamp,long? activity=null,string? comments=null,long? qc=null)=>RequestAsync("POST",$"api/projects/{project}/itps/{version}/{action}",new JsonObject{["expectedStamp"]=stamp,["activityId"]=activity,["comments"]=comments,["qcFormId"]=qc});
 public Task<JsonNode> ItpAuditAsync(long project,long version)=>CachedGetAsync("itp-audit:"+project,version,$"api/projects/{project}/itps/{version}/audit");
}
