using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class FieldWorkflowTests
{
 private sealed class Keys:ILocalKeyStore{public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(new byte[32]);}
 private sealed class Session(Guid owner):ISyncSession{public Guid? Owner=>owner;public Task<string?> GetAccessTokenAsync(CancellationToken ct)=>Task.FromResult<string?>("synthetic-token");}
 private sealed class Handler:HttpMessageHandler
 {
  public List<(string Route,string Body,string? Match,string? Key)> Calls=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   var body=request.Content is null?"":await request.Content.ReadAsStringAsync(ct);
   Calls.Add((request.RequestUri!.AbsolutePath,body,request.Headers.IfMatch.FirstOrDefault()?.Tag,request.Headers.GetValues("Idempotency-Key").Single()));
   var response=new HttpResponseMessage(HttpStatusCode.Created){Content=new StringContent(Calls.Count==1?"{\"evidenceID\":81,\"qcFormID\":71}":"{\"qcSignOffID\":91,\"qcFormID\":71}")};response.Headers.ETag=new("\""+(Calls.Count+4)+"\"");return response;
  }
 }
 [Fact]public async Task Atomic_signature_file_upload_and_signoff_survive_reopen_and_bind_acknowledged_evidence()
 {
  var dir=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());var owner=Guid.NewGuid();var entity=Guid.NewGuid();var keys=new Keys();Guid evidence;
  await using(var store=new OfflineStore(dir,owner,keys))
  {
   await store.CacheAsync(entity,71,"{\"qcFormID\":71,\"status\":\"Submitted\",\"revision\":4}");
   using var stream=new MemoryStream(new byte[]{137,80,78,71,13,10,26,10});
   evidence=await store.SaveEvidenceAsync(entity,stream,"image/png","signature.png",uploadRequest:id=>new SyncRequest("POST","api/qc-forms/71/evidence",EvidenceId:id,EvidenceType:"Signature"),localJson:"{\"qcFormID\":71,\"status\":\"Submitted\",\"revision\":4}",expectedRevision:4,signOffRequest:new SyncRequest("POST","api/qc-forms/71/sign-offs","{\"signOffType\":\"Inspector\"}"));
   Assert.True(await store.HasPendingTransitionAsync(entity));Assert.Equal(2,(await store.CountsAsync())["Pending"]);
  }
  await using(var store=new OfflineStore(dir,owner,keys))
  {
   Assert.Equal(8,(await store.ReadEvidenceAsync(evidence)).Length);var handler=new Handler();using var http=new HttpClient(handler){BaseAddress=new Uri("https://qms.invalid/")};var session=new Session(owner);
   Assert.Equal("Synced",await new SyncEngine(store,session,new HttpSyncTransport(http,store,session)).RunAsync());
   Assert.Equal(2,handler.Calls.Count);Assert.Equal("\"4\"",handler.Calls[0].Match);Assert.Equal("\"5\"",handler.Calls[1].Match);
   Assert.Equal(81,JsonNode.Parse(handler.Calls[1].Body)!["signatureEvidenceID"]!.GetValue<int>());Assert.NotEqual(handler.Calls[0].Key,handler.Calls[1].Key);
   Assert.False(await store.HasPendingTransitionAsync(entity));Assert.Equal(6,JsonNode.Parse((await store.GetAsync(entity))!.Json)!["revision"]!.GetValue<int>());
  }
 }
 [Fact]public async Task Submission_acknowledgement_updates_durable_status_and_revision()
 {
  await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),Guid.NewGuid(),new Keys());var id=Guid.NewGuid();
  await store.SaveAndEnqueueAsync(id,"{\"status\":\"Draft\",\"revision\":2,\"value\":0}","SubmitQCForm","payload",2);
  await store.CompleteAsync((await store.ClaimAsync())!,null,3);
  var json=JsonNode.Parse((await store.GetAsync(id))!.Json)!;Assert.Equal("Submitted",json["status"]!.ToString());Assert.Equal(3,json["revision"]!.GetValue<int>());Assert.Equal(0,json["value"]!.GetValue<int>());
 }
 [Theory][InlineData("Draft",true)][InlineData("ReturnedForCorrection",true)][InlineData("Submitted",false)][InlineData("Approved",false)][InlineData("Rejected",false)]
 public void Editability_matches_server_workflow(string status,bool expected)=>Assert.Equal(expected,FormInput.Editable(status));
 [Fact]public void False_zero_and_na_are_answers_but_empty_values_are_missing()
 {
  Assert.False(FormInput.Missing(JsonValue.Create(false)));Assert.False(FormInput.Missing(JsonValue.Create(0)));Assert.False(FormInput.Missing(JsonValue.Create("N/A")));Assert.True(FormInput.Missing(new JsonArray()));Assert.True(FormInput.Missing(JsonValue.Create(" ")));
 }
 [Fact]public async Task Rejected_approval_can_be_withdrawn_without_deleting_conflict_or_request()
 {
  var dir=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());var owner=Guid.NewGuid();var entity=Guid.NewGuid();Guid id;
  await using(var store=new OfflineStore(dir,owner,new Keys()))
  {
   id=await store.SaveAndEnqueueAsync(entity,"{\"status\":\"Submitted\",\"revision\":3}","ApproveQCForm","approval-request",3);
   await store.FailAsync((await store.ClaimAsync())!,"Missing current signature",false,true,"server-version");
   await store.WithdrawRejectedWorkflowAsync(id,"Provide fresh signing consent first");
   Assert.False(await store.HasPendingTransitionAsync(entity));Assert.False((await store.GetAsync(entity))!.Dirty);
  }
  await using(var store=new OfflineStore(dir,owner,new Keys()))
  {
   var old=Assert.Single(await store.OutboxAsync());Assert.Equal("Cancelled",old.Status);Assert.Equal("approval-request",old.Operation.Payload);
   var history=Assert.Single(await store.ConflictsAsync());Assert.Equal("server-version",history.ServerJson);Assert.Equal("Cancelled",history.Status);
   await store.SaveAndEnqueueAsync(entity,"{\"status\":\"Submitted\"}","CreateSignOff","fresh-consent",3);Assert.Equal("fresh-consent",(await store.ClaimAsync())!.Payload);
  }
 }
 [Fact]public async Task Pending_and_field_value_operations_cannot_be_dismissed_as_rejected_workflow()
 {
  await using var store=new OfflineStore(Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString()),Guid.NewGuid(),new Keys());var entity=Guid.NewGuid();
  var id=await store.SaveAndEnqueueAsync(entity,"local","ApproveQCForm","pending",1);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.WithdrawRejectedWorkflowAsync(id,"unknown result"));
  var field=await store.SaveAndEnqueueAsync(entity,"local","UpdateQCResponse","value",1);await store.FailAsync(new PendingOperation(field,entity,"UpdateQCResponse","value",1,0,null),"Conflict",false,true,"central");
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.WithdrawRejectedWorkflowAsync(field,"dismiss"));Assert.True((await store.GetAsync(entity))!.Dirty);
 }
 [Fact]public void Signature_requires_strokes_and_generates_a_real_png()
 {
  var ink=new SignatureInk();Assert.Throws<InvalidOperationException>(()=>ink.ToPng());ink.Begin(-1,.2f);ink.Move(.8f,2);var png=ink.ToPng();Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},png.Take(8));using var data=new MemoryStream(png);data.Position=8;using var idat=new MemoryStream();var buffer=new byte[8];
  while(data.Position<data.Length){data.ReadExactly(buffer);var length=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(buffer);var type=Encoding.ASCII.GetString(buffer,4,4);var chunk=new byte[length];data.ReadExactly(chunk);data.Position+=4;if(type=="IDAT")idat.Write(chunk);}
  idat.Position=0;using var zip=new System.IO.Compression.ZLibStream(idat,System.IO.Compression.CompressionMode.Decompress);using var decoded=new MemoryStream();zip.CopyTo(decoded);Assert.Equal(240*(600*4+1),decoded.Length);Assert.Contains((byte)15,decoded.ToArray());ink.Clear();Assert.False(ink.HasInk);
 }
 [Fact]public async Task Version_one_dirty_database_upgrades_without_losing_value_operation_or_evidence()
 {
  SQLitePCL.Batteries_V2.Init();var dir=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());Directory.CreateDirectory(dir);var owner=Guid.NewGuid();var entity=Guid.NewGuid();var operation=Guid.NewGuid();var evidence=Guid.NewGuid();var file=Path.Combine(dir,"retained.bin");
  byte[] Encrypt(byte[] bytes){var result=new byte[28+bytes.Length];System.Security.Cryptography.RandomNumberGenerator.Fill(result.AsSpan(0,12));using var aes=new System.Security.Cryptography.AesGcm(new byte[32],16);aes.Encrypt(result.AsSpan(0,12),bytes,result.AsSpan(28),result.AsSpan(12,16),owner.ToByteArray());return result;}
  await File.WriteAllBytesAsync(file,Encrypt(Encoding.UTF8.GetBytes("retained photo")));
  await using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(dir,$"qms-{owner:N}.db")+";Pooling=False"))
  {
   await db.OpenAsync();using var cmd=db.CreateCommand();cmd.CommandText="""
    CREATE TABLE Entity(LocalId TEXT PRIMARY KEY,ServerId INTEGER,Payload BLOB NOT NULL,Dirty INTEGER NOT NULL);
    CREATE TABLE Operation(OperationId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Kind TEXT NOT NULL,Payload BLOB NOT NULL,ExpectedRevision INTEGER,Status TEXT NOT NULL,Attempts INTEGER NOT NULL DEFAULT 0,CreatedAt INTEGER NOT NULL,NextAttempt INTEGER NOT NULL DEFAULT 0,LastError TEXT,DependsOn TEXT REFERENCES Operation(OperationId));
    CREATE TABLE Conflict(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),LocalPayload BLOB NOT NULL,ServerPayload BLOB NOT NULL,Reason TEXT NOT NULL);
    CREATE TABLE Evidence(LocalId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Path TEXT NOT NULL,ContentType TEXT NOT NULL,FileName TEXT NOT NULL);
    INSERT INTO Entity VALUES($entity,71,$value,1);
    INSERT INTO Operation(OperationId,EntityId,Kind,Payload,ExpectedRevision,Status,Attempts,CreatedAt) VALUES($operation,$entity,'UpdateQCResponse',$request,1,'Processing',1,0);
    INSERT INTO Evidence VALUES($evidence,$entity,$file,'image/jpeg','photo.jpg');PRAGMA user_version=1;
    """;
   cmd.Parameters.AddWithValue("$entity",entity.ToString());cmd.Parameters.AddWithValue("$value",Encrypt(Encoding.UTF8.GetBytes("dirty-value")));cmd.Parameters.AddWithValue("$operation",operation.ToString());cmd.Parameters.AddWithValue("$request",Encrypt(Encoding.UTF8.GetBytes("retained-request")));cmd.Parameters.AddWithValue("$evidence",evidence.ToString());cmd.Parameters.AddWithValue("$file",file);await cmd.ExecuteNonQueryAsync();
  }
  await using var store=new OfflineStore(dir,owner,new Keys());Assert.Equal("dirty-value",(await store.GetAsync(entity))!.Json);Assert.True((await store.GetAsync(entity))!.Dirty);Assert.Equal(operation,(await store.ClaimAsync())!.OperationId);Assert.Equal("retained photo",Encoding.UTF8.GetString(await store.ReadEvidenceAsync(evidence)));Assert.Empty(await store.ConflictsAsync());
 }
 [Fact]public async Task Server_rejection_detail_is_retained_encrypted_and_available_after_restart()
 {
  var dir=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString());var owner=Guid.NewGuid();var entity=Guid.NewGuid();
  await using(var store=new OfflineStore(dir,owner,new Keys())){await store.SaveAndEnqueueAsync(entity,"local","ApproveQCForm","request",1);await store.FailAsync((await store.ClaimAsync())!,"Conflict",false,true,"central",failureJson:"{\"detail\":\"Fresh signature required\"}");}
  await using(var store=new OfflineStore(dir,owner,new Keys()))Assert.Contains("Fresh signature required",Assert.Single(await store.OutboxAsync()).FailureJson);
  var bytes=await File.ReadAllBytesAsync(Path.Combine(dir,$"qms-{owner:N}.db"));Assert.DoesNotContain("Fresh signature required",Encoding.UTF8.GetString(bytes));
 }
 [Fact]public void Local_numeric_ranges_are_checked_before_enqueue()
 {var field=JsonNode.Parse("{\"fieldType\":\"Decimal\",\"validationRules\":{\"min\":0,\"max\":10}}")!;FormInput.Validate(field,JsonValue.Create(0));Assert.Throws<ArgumentException>(()=>FormInput.Validate(field,JsonValue.Create(11)));}
 [Fact]public void Local_text_limits_are_checked_before_enqueue()
 {var field=JsonNode.Parse("{\"fieldType\":\"Text\",\"validationRules\":{\"maxLength\":3}}")!;Assert.Throws<ArgumentException>(()=>FormInput.Validate(field,JsonValue.Create("long")));}
 [Fact]public void Local_choice_validation_rejects_unknown_options()
 {var field=JsonNode.Parse("{\"fieldType\":\"Dropdown\",\"options\":[\"Pass\",\"Fail\",\"N/A\"]}")!;FormInput.Validate(field,JsonValue.Create("N/A"));Assert.Throws<ArgumentException>(()=>FormInput.Validate(field,JsonValue.Create("unknown")));}
 [Fact]public void Required_false_and_zero_are_preserved_in_local_validation()
 {FormInput.Validate(JsonNode.Parse("{\"fieldType\":\"Boolean\",\"isRequired\":true}")!,JsonValue.Create(false));FormInput.Validate(JsonNode.Parse("{\"fieldType\":\"Number\",\"isRequired\":true}")!,JsonValue.Create(0));}
 [Fact]public void Required_photos_and_values_are_validated_before_submission()
 {
  var form=JsonNode.Parse("{\"projectFields\":[{\"fieldLabel\":\"Area\",\"isRequired\":true}],\"sections\":[{\"fields\":[{\"qcFormFieldID\":7,\"fieldLabel\":\"Photo\",\"fieldType\":\"Photo\",\"isRequired\":true}]}]}")!;
  Assert.Equal(2,FormInput.RequiredErrors(form,new HashSet<long>()).Count);form["projectFields"]![0]!["value"]="Area 1";Assert.Empty(FormInput.RequiredErrors(form,new HashSet<long>{7}));
 }
}
