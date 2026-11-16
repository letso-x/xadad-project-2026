using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class SnapshotContractRegressionTests
{
 private static JsonNode Definition()=>JsonNode.Parse("""
 {"checklistTemplateID":3,"checklistTemplateVersionID":4,"templateNumber":"TEST","templateName":"Inspection","isTemplateActive":true,"revision":"01","requiredSignOffTypes":["Inspector"],"projectFields":[{"fieldKey":"area","fieldLabel":"Area","fieldType":"Reference","isRequired":true,"options":null,"validationRules":{"maxLength":50}}],"sections":[{"sectionName":"Checks","isRequired":true,"fields":[{"fieldKey":"outcome","fieldLabel":"Equipment installed","fieldType":"Dropdown","isRequired":true,"options":["A","R","N/A"],"validationRules":{"inspectionItem":{"key":"outcome","role":"Result"}}}]}]}
 """)!;
 private static JsonNode Local()=>OfflineFormIdentity.Create(-99,2,Definition(),new JsonObject{["projectName"]="Test project"},Guid.NewGuid());
 private static JsonNode Broken()
 {
  var local=Local();local["projectFields"]![0]!["validationRules"]=new JsonObject();var field=local["sections"]![0]!["fields"]![0]!;
  field["options"]=new JsonArray();field["validationRules"]=new JsonObject();field["value"]="R";
  local["projectFields"]![0]!["value"]="Retained area";local["remarks"]="Retained remarks";return local;
 }
 private static JsonNode Server()
 {
  var server=Local();server["qcFormID"]=10;server["formNumber"]="QC-TEST";server["projectFields"]![0]!["qcFormProjectFieldID"]=1;
  server["sections"]![0]!["qcFormSectionID"]=1;var field=server["sections"]![0]!["fields"]![0]!;field["qcFormFieldID"]=1;field["qcResponseID"]=1;return server;
 }
 [Fact]public void Actual_api_parsed_json_contract_survives_template_revision_and_offline_creation()
 {
  var definition=Definition();var before=definition.ToJsonString();var revision=TemplateRevisionDraft.FromDefinition(definition);
  Assert.Equal(50,revision["projectFields"]![0]!["validationRules"]!["maxLength"]!.GetValue<int>());
  var form=Local();var result=form["sections"]![0]!["fields"]![0]!;
  Assert.True(ChecklistItems.IsResultOptions(result["options"]!.AsArray().Select(o=>o!.ToString()).ToArray()));
  Assert.Equal("Result",ChecklistItems.Role(result));Assert.Equal(before,definition.ToJsonString());
 }
 [Fact]public void Repair_restores_only_missing_metadata_and_preserves_existing_answers_and_identities()
 {
  var broken=Broken();var before=broken.ToJsonString();var repaired=OfflineFormIdentity.RestoreMetadata(broken,Definition());
  Assert.False(OfflineFormIdentity.NeedsMetadataRepair(repaired));Assert.Equal(before,broken.ToJsonString());
  Assert.Equal("R",repaired["sections"]![0]!["fields"]![0]!["value"]!.ToString());Assert.Equal("Retained area",repaired["projectFields"]![0]!["value"]!.ToString());
  Assert.Equal("Retained remarks",repaired["remarks"]!.ToString());Assert.Equal(broken["sections"]![0]!["fields"]![0]!["qcResponseID"]!.ToString(),repaired["sections"]![0]!["fields"]![0]!["qcResponseID"]!.ToString());
  Assert.Equal("Result",ChecklistItems.Role(repaired["sections"]![0]!["fields"]![0]!));
  Assert.NotNull(OfflineFormIdentity.Acknowledge(repaired,Server())["serverIdMap"]);
 }
 [Fact]public void Wrong_revision_or_changed_existing_metadata_is_never_silently_accepted()
 {
  var broken=Broken();var definition=Definition();definition["checklistTemplateVersionID"]=999;
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.RestoreMetadata(broken,definition));
  var repaired=OfflineFormIdentity.RestoreMetadata(broken,Definition());var server=Server();server["revision"]=1;
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Acknowledge(repaired,server));
  broken["sections"]![0]!["fields"]![0]!["validationRules"]=new JsonObject{["differentRule"]=true};
  repaired=OfflineFormIdentity.RestoreMetadata(broken,Definition());Assert.True(repaired["sections"]![0]!["fields"]![0]!["validationRules"]!["differentRule"]!.GetValue<bool>());
  Assert.Throws<InvalidOperationException>(()=>OfflineFormIdentity.Acknowledge(repaired,Server()));
 }
 private sealed class Keys:ILocalKeyStore{private readonly byte[] key=RandomNumberGenerator.GetBytes(32);public Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)=>Task.FromResult(key.ToArray());}
 [Fact]public async Task Metadata_repair_keeps_outbox_payloads_answers_and_failure_history_across_restart()
 {
  var directory=Path.Combine(Path.GetTempPath(),"qms-tests",Guid.NewGuid().ToString("N"));var owner=Guid.NewGuid();var entity=Guid.NewGuid();var keys=new Keys();Guid createId,answerId;var json=Broken().ToJsonString();
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   createId=await store.SaveAndEnqueueAsync(entity,json,"CreateQCForm",JsonSerializer.Serialize(new SyncRequest("POST","api/qc-forms","{}")),null);
   answerId=await store.SaveAndEnqueueAsync(entity,json,"UpdateQCResponse",JsonSerializer.Serialize(new SyncRequest("PUT","api/qc-forms/-99/responses/-3","{\"value\":\"R\"}")),0);
   var create=(await store.ClaimAsync())!;await store.FailAsync(create,"Created record differs from saved checklist; review both versions",false,true,Server().ToJsonString(),failureJson:"{\"qcFormID\":10}");
   var before=(await store.OutboxAsync()).Select(o=>(o.Operation.OperationId,o.Operation.Payload)).ToArray();
   await store.RestoreFormMetadataAsync(entity,Definition());Assert.True((await store.GetAsync(entity))!.Dirty);
   Assert.Equal(before,(await store.OutboxAsync()).Select(o=>(o.Operation.OperationId,o.Operation.Payload)).ToArray());
   await store.CompleteAsync(create,10,0,resultJson:"{\"qcFormID\":10}",serverSnapshot:Server().ToJsonString());
  }
  await using(var store=new OfflineStore(directory,owner,keys))
  {
   var record=(await store.GetAsync(entity))!;var form=JsonNode.Parse(record.Json)!;
   Assert.Equal(10,record.ServerId);Assert.Equal("R",form["sections"]![0]!["fields"]![0]!["value"]!.ToString());Assert.True(record.Dirty);
   Assert.Equal(answerId,(await store.ClaimAsync())!.OperationId);Assert.Equal("Completed",Assert.Single(await store.ConflictsAsync()).Status);
   Assert.Equal(createId,Assert.Single(await store.ConflictsAsync()).OperationId);
  }
 }
}