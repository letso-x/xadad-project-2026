using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class OfflineFormIdentityTests
{
 private sealed class Keys:ILocalKeyStore{private readonly byte[] key=RandomNumberGenerator.GetBytes(32);public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(key.ToArray());}
 private static JsonNode Local()=>JsonNode.Parse("""
 {"qcFormID":-9000,"projectID":2,"checklistTemplateID":3,"checklistTemplateVersionID":4,"localCreate":true,"formNumber":"Pending","status":"Draft","revision":0,"projectFields":[{"qcFormProjectFieldID":-1,"fieldKey":"area","fieldType":"Text","value":"North"}],"sections":[{"qcFormSectionID":-4,"fields":[{"qcFormFieldID":-2,"qcResponseID":-3,"fieldKey":"result","fieldType":"Dropdown","value":"A"}]}]}
 """)!;
 private static JsonNode Server()=>JsonNode.Parse("""
 {"qcFormID":10,"projectID":2,"checklistTemplateID":3,"checklistTemplateVersionID":4,"formNumber":"QC-TEST","status":"Draft","revision":0,"documentSnapshot":{"revision":"Rev 1"},"projectFields":[{"qcFormProjectFieldID":1,"fieldKey":"area","fieldType":"Text","value":null}],"sections":[{"qcFormSectionID":1,"fields":[{"qcFormFieldID":1,"qcResponseID":1,"fieldKey":"result","fieldType":"Dropdown","value":null}]}]}
 """)!;
 [Fact] public void Overlapping_server_table_ids_translate_by_identity_type_and_keep_saved_answers()
 {
  var acknowledged=OfflineFormIdentity.Acknowledge(Local(),Server());Assert.Equal("A",acknowledged["sections"]![0]!["fields"]![0]!["value"]!.ToString());
  Assert.Equal("api/qc-forms/10/responses/1",OfflineFormIdentity.Resolve(new("PUT","api/qc-forms/-9000/responses/-3"),acknowledged).Route);
  Assert.Equal(1,OfflineFormIdentity.Resolve(new("POST","api/qc-forms/-9000/evidence",QCFormFieldID:-2),acknowledged).QCFormFieldID);
  var display=OfflineFormIdentity.Localize(Server(),acknowledged);
  Assert.Equal("-2",display["sections"]![0]!["fields"]![0]!["qcFormFieldID"]!.ToString());Assert.Equal("-3",display["sections"]![0]!["fields"]![0]!["qcResponseID"]!.ToString());
 }
 [Fact] public void Mismatched_project_or_template_fails_without_mutating_local_work()
 {
  var local=Local();var before=local.ToJsonString();var server=Server();server["checklistTemplateVersionID"]=999;
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Acknowledge(local,server));Assert.Equal(before,local.ToJsonString());
 }
 [Fact] public void Missing_or_changed_field_blocks_dependent_requests()
 {
  var server=Server();server["sections"]![0]!["fields"]![0]!["fieldKey"]="different";
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Acknowledge(Local(),server));
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Resolve(new("PUT","api/qc-forms/-9000/responses/-3"),Local()));
 }
 [Fact] public async Task Restart_retains_mapping_answers_photo_dependency_and_late_input_cannot_erase_it()
 {
  var directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));var owner=Guid.NewGuid();var entity=Guid.NewGuid();var keys=new Keys();Guid createId;
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   createId=await store.SaveAndEnqueueAsync(entity,Local().ToJsonString(),"CreateQCForm",JsonSerializer.Serialize(new SyncRequest("POST","api/qc-forms","{}")),null);
   await store.SaveAndEnqueueAsync(entity,Local().ToJsonString(),"UpdateQCResponse",JsonSerializer.Serialize(new SyncRequest("PUT","api/qc-forms/-9000/responses/-3","{\"value\":\"A\"}")),0);
   using var photo=new MemoryStream([137,80,78,71]);await store.SaveEvidenceAsync(entity,photo,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/-9000/evidence",EvidenceId:id,QCFormFieldID:-2),localJson:Local().ToJsonString(),expectedRevision:0);
   var create=await store.ClaimAsync();Assert.Equal(createId,create!.OperationId);Assert.Null(await store.ClaimAsync());
   await store.CompleteAsync(create,10,0,resultJson:"{\"qcFormID\":10}",serverSnapshot:Server().ToJsonString());
   var late=Local();late["remarks"]="Late retained input";await store.SaveAndEnqueueAsync(entity,late.ToJsonString(),"UpdateRemarks",JsonSerializer.Serialize(new SyncRequest("PUT","api/qc-forms/-9000/remarks","{}")),0);
  }
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   var local=JsonNode.Parse((await store.GetAsync(entity))!.Json)!;Assert.NotNull(local["serverIdMap"]);Assert.Equal("Late retained input",local["remarks"]!.ToString());
   var answer=await store.ClaimAsync();Assert.Equal("UpdateQCResponse",answer!.Kind);Assert.Equal("api/qc-forms/10/responses/1",OfflineFormIdentity.Resolve(JsonSerializer.Deserialize<SyncRequest>(answer.Payload)!,local).Route);
   await store.CompleteAsync(answer,null,1);var upload=await store.ClaimAsync();Assert.Equal("UploadEvidence",upload!.Kind);Assert.Equal(1,upload.ExpectedRevision);
   var resolved=OfflineFormIdentity.Resolve(JsonSerializer.Deserialize<SyncRequest>(upload.Payload)!,local);Assert.Equal(1,resolved.QCFormFieldID);Assert.Equal(new byte[]{137,80,78,71},await store.ReadEvidenceAsync(resolved.EvidenceId!.Value));
  }
 }
 private sealed class Session(Guid owner):ISyncSession{public Guid? Owner=>owner;public Task<string?> GetAccessTokenAsync(CancellationToken ct)=>Task.FromResult<string?>("synthetic-token");}
 private sealed class ReplyTransport(SyncReply reply):ISyncTransport{public Task<SyncReply> SendAsync(PendingOperation operation,CancellationToken ct)=>Task.FromResult(reply);}
 [Fact] public async Task Changed_creation_preserves_both_versions_as_a_conflict_and_blocks_dependents()
 {
  var directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));var owner=Guid.NewGuid();var entity=Guid.NewGuid();await using var store=new OfflineStore(directory,owner,new Keys());
  await store.SaveAndEnqueueAsync(entity,Local().ToJsonString(),"CreateQCForm",JsonSerializer.Serialize(new SyncRequest("POST","api/qc-forms","{}")),null);
  var server=Server();server["revision"]=1L;var result=await new SyncEngine(store,new Session(owner),new ReplyTransport(new(201,"{\"qcFormID\":10}",0,10,server.ToJsonString()))).RunAsync();
  Assert.Equal("Conflict",result);Assert.Single(await store.ConflictsAsync());Assert.Equal(Local().ToJsonString(),(await store.GetAsync(entity))!.Json);Assert.Null(await store.ClaimAsync());
 }
 private sealed class Recorder:HttpMessageHandler
 {
  public List<(string Route,string Body,string Key)> Requests {get;}=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {Requests.Add((request.RequestUri!.AbsolutePath,await request.Content!.ReadAsStringAsync(ct),request.Headers.GetValues("Idempotency-Key").Single()));return new(System.Net.HttpStatusCode.OK){Content=new StringContent("{}")};}
 }
 [Fact] public async Task Multipart_retry_translates_identities_without_changing_operation_or_bytes()
 {
  var directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));var owner=Guid.NewGuid();var entity=Guid.NewGuid();await using var store=new OfflineStore(directory,owner,new Keys());
  var local=OfflineFormIdentity.Acknowledge(Local(),Server());await store.CacheAsync(entity,10,local.ToJsonString());using var image=new MemoryStream(new byte[]{137,80,78,71});
  await store.SaveEvidenceAsync(entity,image,"image/png","photo.png",uploadRequest:id=>new("POST","api/qc-forms/-9000/evidence",EvidenceId:id,EvidenceType:"Photo",QCFormFieldID:-2),localJson:local.ToJsonString(),expectedRevision:0);
  var operation=await store.ClaimAsync();var payload=operation!.Payload;var recorder=new Recorder();using var client=new HttpClient(recorder){BaseAddress=new Uri("https://api.invalid/")};var transport=new HttpSyncTransport(client,store,new Session(owner));
  await transport.SendAsync(operation,default);await transport.SendAsync(operation,default);
  Assert.Equal(recorder.Requests[0],recorder.Requests[1]);Assert.Equal("/api/qc-forms/10/evidence",recorder.Requests[0].Route);Assert.DoesNotContain("-2",recorder.Requests[0].Body);Assert.Equal(payload,operation.Payload);
 }
 [Fact] public void Changed_photo_requirement_cannot_be_silently_accepted_during_creation_acknowledgement()
 {
  var local=Local();var server=Server();server["sections"]![0]!["fields"]![0]!["requiresPhoto"]=true;
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Acknowledge(local,server));Assert.Equal("A",local["sections"]![0]!["fields"]![0]!["value"]!.ToString());
 }
}
