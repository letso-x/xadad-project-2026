using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace MzukuluQMS.Mobile.Core;
public sealed record SyncRequest(string Method,string Route,string? Body=null,Guid? EvidenceId=null,string? EvidenceType=null,long? QCFormFieldID=null);
public sealed record SyncReply(int Status,string Body,long? Revision,long? ServerId,string? ServerSnapshot=null);
public interface ISyncTransport { Task<SyncReply> SendAsync(PendingOperation operation,CancellationToken ct); }
public interface ISyncSession { Guid? Owner {get;} Task<string?> GetAccessTokenAsync(CancellationToken ct); }
public sealed class SyncEngine(OfflineStore store,ISyncSession session,ISyncTransport transport)
{
 private readonly SemaphoreSlim gate=new(1,1);
 public async Task<string> RunAsync(CancellationToken ct=default)
 {
  if(!await gate.WaitAsync(0,ct))return "Syncing";
  try
  {
   if(session.Owner!=store.Owner || string.IsNullOrEmpty(await session.GetAccessTokenAsync(ct)))return "Authentication required";
   while(true)
   {
    var operation=await store.ClaimAsync(ct);if(operation is null)break;
    try
    {
     var reply=await transport.SendAsync(operation,ct);
     if(reply.Status is >=200 and <300)await store.CompleteAsync(operation,reply.ServerId,reply.Revision,ct);
     else if(reply.Status==401){await store.FailAsync(operation,"Authentication required",true,ct:ct);return "Authentication required";}
     else if(reply.Status==409)await store.FailAsync(operation,"Central record changed; review conflict",false,true,reply.ServerSnapshot??reply.Body,ct);
     else await store.FailAsync(operation,"API returned HTTP "+reply.Status,reply.Status>=500 && operation.Attempts<8,ct:ct);
    }
    catch(OperationCanceledException)
    {
     await store.FailAsync(operation,"Sync interrupted",true,ct:CancellationToken.None);throw;
    }
    catch(HttpRequestException){await store.FailAsync(operation,"API unavailable",operation.Attempts<8,ct:ct);}
    catch(Exception error) when(error is InvalidOperationException or JsonException or ArgumentException or IOException)
    {await store.FailAsync(operation,"Queued operation needs review; local data retained",false,ct:ct);}
   }
   var counts=await store.CountsAsync(ct);
   if(counts.GetValueOrDefault("Conflict")>0)return "Conflict";
   if(counts.GetValueOrDefault("Failed")>0)return "Sync failed";
   return counts.GetValueOrDefault("Pending")+counts.GetValueOrDefault("Processing")>0?"Pending sync":"Synced";
  }
  finally{gate.Release();}
 }
}
public sealed class HttpSyncTransport(HttpClient client,OfflineStore store,ISyncSession session) : ISyncTransport
{
 public async Task<SyncReply> SendAsync(PendingOperation operation,CancellationToken ct)
 {
  if(session.Owner!=store.Owner)throw new InvalidOperationException("Local data belongs to another account.");
  var request=JsonSerializer.Deserialize<SyncRequest>(operation.Payload)??throw new InvalidOperationException("Invalid queued request.");
  // Restrict durable payloads to same-origin QC API routes; never transmit to a posted absolute URI.
  if(!request.Route.StartsWith("api/qc-forms",StringComparison.Ordinal) || request.Route.Contains(":") || request.Route.Contains(".."))throw new InvalidOperationException("Invalid queued API route.");
  using var message=new HttpRequestMessage(new HttpMethod(request.Method),request.Route);
  var token=await session.GetAccessTokenAsync(ct);
  if(string.IsNullOrWhiteSpace(token))return new(401,"",null,null);
  message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  message.Headers.Add("Idempotency-Key",operation.OperationId.ToString());
  if(operation.ExpectedRevision.HasValue)message.Headers.IfMatch.Add(new EntityTagHeaderValue("\""+operation.ExpectedRevision+"\""));
  if(request.EvidenceId.HasValue)
  {
   // Stable boundary and field order are part of byte-for-byte server fingerprint replay.
   var multipart=new MultipartFormDataContent("qms-"+operation.OperationId.ToString("N"));
   multipart.Add(new StringContent(request.EvidenceType??"Photo"),"EvidenceType");
   if(request.QCFormFieldID.HasValue)multipart.Add(new StringContent(request.QCFormFieldID.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),"QCFormFieldID");
   var bytes=await store.ReadEvidenceAsync(request.EvidenceId.Value,ct);var file=new ByteArrayContent(bytes);
   var png=bytes.Length>=4 && bytes[0]==137 && bytes[1]==80;file.Headers.ContentType=new MediaTypeHeaderValue(png?"image/png":"image/jpeg");
   multipart.Add(file,"File",png?"evidence.png":"evidence.jpg");message.Content=multipart;
  }
  else if(request.Body is not null)message.Content=new StringContent(request.Body,Encoding.UTF8,"application/json");
  using var response=await client.SendAsync(message,ct);var body=await response.Content.ReadAsStringAsync(ct);
  long? revision=long.TryParse(response.Headers.ETag?.Tag.Trim('"'),out var parsed)?parsed:null;
  long? serverId=null;
  if(response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
  {
   using var doc=JsonDocument.Parse(body);
   foreach(var property in doc.RootElement.EnumerateObject())if(property.Name.Equals("qcFormID",StringComparison.OrdinalIgnoreCase) && property.Value.TryGetInt64(out var id)){serverId=id;break;}
  }
  string? snapshot=null;
  if(response.StatusCode==HttpStatusCode.Conflict)
  {
   var parts=request.Route.Split('/');
   if(parts.Length>=3 && long.TryParse(parts[2],out _))
   {
    using var get=new HttpRequestMessage(HttpMethod.Get,string.Join('/',parts.Take(3)));get.Headers.Authorization=message.Headers.Authorization;
    using var current=await client.SendAsync(get,ct);if(current.IsSuccessStatusCode)snapshot=await current.Content.ReadAsStringAsync(ct);
   }
  }
  return new((int)response.StatusCode,body,revision,serverId,snapshot);
 }
}
