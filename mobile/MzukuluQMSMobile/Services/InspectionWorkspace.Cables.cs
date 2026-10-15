using System.Text.Json.Nodes;
namespace MzukuluQMSMobile.Services;
public sealed partial class InspectionWorkspace
{
 public Task<JsonNode> CableRegistersAsync(long projectId)=>CachedGetAsync("cable-registers",projectId,$"api/projects/{projectId}/cable-registers");
 public Task<JsonNode> CableRegisterAsync(long projectId,long registerId)=>CachedGetAsync("cable-register:"+projectId,registerId,$"api/projects/{projectId}/cable-registers/{registerId}");
 public Task<JsonNode> SaveCableRegisterAsync(long projectId,long? registerId,JsonNode body)=>RequestAsync(registerId.HasValue?"PUT":"POST",$"api/projects/{projectId}/cable-registers"+(registerId.HasValue?$"/{registerId}":""),body);
 public Task<JsonNode> SaveCableAsync(long projectId,long registerId,long? cableId,long revision,JsonNode body)=>RequestAsync(cableId.HasValue?"PUT":"POST",$"api/projects/{projectId}/cable-registers/{registerId}/cables"+(cableId.HasValue?$"/{cableId}":"")+$"?registerRevision={revision}",body);
 public Task<JsonNode> VerifyCableRegisterAsync(long projectId,long registerId,long revision)=>RequestAsync("POST",$"api/projects/{projectId}/cable-registers/{registerId}/verify?revision={revision}");
}
