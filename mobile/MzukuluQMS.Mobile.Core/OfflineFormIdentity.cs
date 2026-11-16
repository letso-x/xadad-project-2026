using System.Globalization;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;

// Temporary identities remain stable in the local renderer and draft keys. Only transport identities are translated.
public static class OfflineFormIdentity
{
 private static readonly string[] IdKeys=["qcFormID","qcFormProjectFieldID","qcFormSectionID","qcFormFieldID","qcResponseID"];
 public static JsonObject Create(long temporaryId,long projectId,JsonNode template,JsonNode project,Guid actor)
 {
  if(temporaryId>=0||projectId<=0)throw new ArgumentException("Invalid local form identity.");
  var draft=TemplateRevisionDraft.FromDefinition(template);long next=0;
  JsonObject Field(JsonNode input,bool header)
  {
   var field=new JsonObject{["fieldKey"]=input["key"]!.DeepClone(),["fieldLabel"]=input["label"]!.DeepClone(),["fieldType"]=input["type"]!.DeepClone(),
    ["isRequired"]=input["required"]!.DeepClone(),["requiresPhoto"]=input["requiresPhoto"]!.DeepClone(),["requiresSignature"]=input["requiresSignature"]!.DeepClone(),
    ["options"]=input["options"]!.DeepClone(),["validationRules"]=input["validationRules"]!.DeepClone(),["helpText"]=input["helpText"]?.DeepClone(),["placeholder"]=input["placeholder"]?.DeepClone(),["value"]=null};
   field[header?"qcFormProjectFieldID":"qcFormFieldID"]=--next;if(!header)field["qcResponseID"]=--next;return field;
  }
  var projectFields=new JsonArray();foreach(var f in draft["projectFields"]!.AsArray())projectFields.Add(Field(f!,true));
  var sections=new JsonArray();foreach(var section in draft["sections"]!.AsArray())
  {
   var fields=new JsonArray();foreach(var f in section!["fields"]!.AsArray())fields.Add(Field(f!,false));
   sections.Add(new JsonObject{["qcFormSectionID"]=--next,["sectionName"]=section!["title"]?.DeepClone(),["isRequired"]=section["required"]!.DeepClone(),["fields"]=fields});
  }
  var now=DateTimeOffset.UtcNow.ToString("O");
  return new JsonObject{["qcFormID"]=temporaryId,["localCreate"]=true,["projectID"]=projectId,["checklistTemplateID"]=template["checklistTemplateID"]!.DeepClone(),
   ["checklistTemplateVersionID"]=template["checklistTemplateVersionID"]!.DeepClone(),["formNumber"]="Saved on this device · awaiting record number",["status"]="Draft",["revision"]=0L,
   ["startedByUserID"]=actor.ToString(),["createdAt"]=now,["updatedAt"]=now,["projectFields"]=projectFields,["sections"]=sections,["requiredSignOffTypes"]=draft["requiredSignOffTypes"]!.DeepClone(),
   ["documentSnapshot"]=new JsonObject{["documentNumber"]=draft["documentNumber"]!.DeepClone(),["checklistName"]=draft["documentName"]!.DeepClone(),["revision"]=template["revision"]?.DeepClone(),
    ["scope"]=draft["scope"]?.DeepClone(),["acceptanceCriteria"]=draft["acceptanceCriteria"]?.DeepClone(),["project"]=project["projectName"]?.DeepClone(),["projectNumber"]=project["projectNumber"]?.DeepClone(),
    ["client"]=project["clientName"]?.DeepClone(),["contractOrderNumber"]=project["contractOrderNumber"]?.DeepClone(),["siteLocation"]=project["siteLocation"]?.DeepClone()}};
 }
 // Repair only the known local snapshot contract loss, using its immutable pinned revision.
 // Answers, local identities, signatures, queue payloads and nonempty metadata are never replaced.
 public static bool NeedsMetadataRepair(JsonNode local)=>local["localCreate"]?.GetValue<bool>()==true&&FormInput.Editable(local["status"]?.ToString()??"")&&local["serverIdMap"] is null&&
  (local["sections"]?.AsArray().SelectMany(s=>s?["fields"]?.AsArray()??[]).Any(f=>f?["fieldType"]?.ToString()=="Dropdown"&&f["options"] is JsonArray a&&a.Count==0)==true);
 public static JsonObject RestoreMetadata(JsonNode local,JsonNode definition)
 {
  if(!NeedsMetadataRepair(local))return local.DeepClone().AsObject();
  foreach(var key in new[]{"checklistTemplateID","checklistTemplateVersionID"})
   if(local[key]?.ToString()!=definition[key]?.ToString())throw new InvalidOperationException("The saved checklist revision does not match. Your answers are retained.");
  var draft=TemplateRevisionDraft.FromDefinition(definition);var result=local.DeepClone().AsObject();
  void Restore(JsonArray fields,JsonArray source)
  {
   if(fields.Count!=source.Count)throw new InvalidOperationException("The saved checklist fields do not match. Your answers are retained.");
   foreach(var field in fields)
   {
    var match=source.SingleOrDefault(f=>f?["key"]?.ToString()==field?["fieldKey"]?.ToString());
    if(match is null||match["type"]?.ToString()!=field?["fieldType"]?.ToString())throw new InvalidOperationException("The saved checklist field does not match. Your answers are retained.");
    if(field!["options"] is JsonArray options&&options.Count==0)field["options"]=match["options"]!.DeepClone();
    if(field["validationRules"] is JsonObject rules&&rules.Count==0)field["validationRules"]=match["validationRules"]!.DeepClone();
   }
  }
  Restore(result["projectFields"]!.AsArray(),draft["projectFields"]!.AsArray());
  var sections=result["sections"]!.AsArray();var originals=draft["sections"]!.AsArray();
  if(sections.Count!=originals.Count)throw new InvalidOperationException("The saved checklist sections do not match. Your answers are retained.");
  for(var i=0;i<sections.Count;i++)Restore(sections[i]!["fields"]!.AsArray(),originals[i]!["fields"]!.AsArray());
  return result;
 }
 public static JsonObject Acknowledge(JsonNode local,JsonNode server)
 {
  foreach(var key in new[]{"projectID","checklistTemplateID","checklistTemplateVersionID"})if(local[key]?.ToString()!=server[key]?.ToString())throw new InvalidOperationException("Created record does not match the saved project and template. Local work is retained for review.");
  if(server["revision"]?.ToString()!="0"||server["status"]?.ToString() is not ("Draft" or "InProgress"))throw new InvalidOperationException("The new central record was already changed. Local work is retained for review.");
  var result=local.DeepClone().AsObject();var map=new JsonObject();foreach(var key in IdKeys)map[key]=new JsonObject();
  void Pair(JsonNode a,JsonNode b,string key)
  {
   if(!long.TryParse(a[key]?.ToString(),out var old)||old>=0||!long.TryParse(b[key]?.ToString(),out var id)||id<=0)throw new InvalidOperationException("The new record's identities are incomplete. Local work is retained.");
   map[key]![old.ToString(CultureInfo.InvariantCulture)]=id;
  }
  void Fields(JsonArray a,JsonArray b,bool header)
  {
   if(a.Count!=b.Count)throw new InvalidOperationException("The created checklist differs from the saved checklist. Local work is retained for review.");
   var byKey=b.ToDictionary(f=>f!["fieldKey"]!.ToString(),StringComparer.Ordinal);
   foreach(var f in a)
   {
    if(!byKey.TryGetValue(f!["fieldKey"]!.ToString(),out var match)||f["fieldType"]?.ToString()!=match!["fieldType"]?.ToString())throw new InvalidOperationException("The created checklist fields differ. Local work is retained.");
    if(f["fieldLabel"]?.ToString()!=match!["fieldLabel"]?.ToString())throw new InvalidOperationException("The created inspection label differs from the saved checklist. Local work is retained.");
    foreach(var flag in new[]{"isRequired","requiresPhoto","requiresSignature"})if((f[flag]?.GetValue<bool>()??false)!=(match[flag]?.GetValue<bool>()??false))throw new InvalidOperationException("The created inspection requirements differ. Local work is retained.");
    if(!JsonNode.DeepEquals(f["validationRules"]??new JsonObject(),match["validationRules"]??new JsonObject())||!JsonNode.DeepEquals(f["options"]??new JsonArray(),match["options"]??new JsonArray()))throw new InvalidOperationException("The created inspection rules differ. Local work is retained.");
    Pair(f,match!,header?"qcFormProjectFieldID":"qcFormFieldID");if(!header)Pair(f,match!,"qcResponseID");
   }
  }
  Pair(local,server,"qcFormID");Fields(local["projectFields"]!.AsArray(),server["projectFields"]!.AsArray(),true);
  var sections=local["sections"]!.AsArray();var serverSections=server["sections"]!.AsArray();if(sections.Count!=serverSections.Count)throw new InvalidOperationException("The created checklist sections differ. Local work is retained.");
  for(var i=0;i<sections.Count;i++){Pair(sections[i]!,serverSections[i]!,"qcFormSectionID");Fields(sections[i]!["fields"]!.AsArray(),serverSections[i]!["fields"]!.AsArray(),false);}
  result["serverIdMap"]=map;result["serverFormID"]=server["qcFormID"]!.DeepClone();result["formNumber"]=server["formNumber"]!.DeepClone();result["localCreate"]=false;result["status"]=server["status"]!.DeepClone();
  result["revision"]=server["revision"]!.DeepClone();result["documentSnapshot"]=server["documentSnapshot"]?.DeepClone();return result;
 }
 public static SyncRequest Resolve(SyncRequest request,JsonNode local)
 {
  if(local["serverIdMap"] is not JsonObject map)throw new InvalidOperationException("The new QC record has not yet been acknowledged. Saved work is retained.");
  long ConvertId(string key,long value)=>value>=0?value:map[key]?[value.ToString(CultureInfo.InvariantCulture)]?.GetValue<long>()??throw new InvalidOperationException("An answer or photo identity could not be reconciled. Saved work is retained.");
  var parts=request.Route.Split('/');if(parts.Length<3||parts[0]!="api"||parts[1]!="qc-forms")throw new InvalidOperationException("Invalid local record route.");
  if(long.TryParse(parts[2],out var form))parts[2]=ConvertId("qcFormID",form).ToString(CultureInfo.InvariantCulture);
  if(parts.Length>=5&&long.TryParse(parts[4],out var target)&&parts[3] is "responses" or "project-fields")parts[4]=ConvertId(parts[3]=="responses"?"qcResponseID":"qcFormProjectFieldID",target).ToString(CultureInfo.InvariantCulture);
  return request with {Route=string.Join('/',parts),QCFormFieldID=request.QCFormFieldID is long field?ConvertId("qcFormFieldID",field):null};
 }
 public static JsonNode Localize(JsonNode server,JsonNode local)
 {
  var result=server.DeepClone();var map=local["serverIdMap"]!.AsObject();
  void Visit(JsonNode? node)
  {
   if(node is JsonObject obj)foreach(var property in obj.ToArray())
   {
    if(IdKeys.Contains(property.Key)&&property.Value is not null&&map[property.Key] is JsonObject ids)
    {var old=ids.FirstOrDefault(p=>p.Value?.ToString()==property.Value.ToString());if(old.Key is not null)obj[property.Key]=long.Parse(old.Key,CultureInfo.InvariantCulture);}
    else Visit(property.Value);
   }
   else if(node is JsonArray array)foreach(var child in array)Visit(child);
  }
  Visit(result);if(result is JsonObject form&&form["sections"] is not null){form["serverIdMap"]=map.DeepClone();form["serverFormID"]=local["serverFormID"]!.DeepClone();form["localCreate"]=false;}
  return result;
 }
}
