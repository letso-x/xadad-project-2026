using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;

public static class TemplateRevisionDraft
{
 public static JsonObject FromDefinition(JsonNode definition)
 {
  if(!long.TryParse(definition["checklistTemplateVersionID"]?.ToString(),out var version)||version<=0||!long.TryParse(definition["checklistTemplateID"]?.ToString(),out var template)||template<=0)
   throw new ArgumentException("Select a published template revision.");
  JsonObject Field(JsonNode f)=>new(){["key"]=f["fieldKey"]?.DeepClone(),["label"]=f["fieldLabel"]?.DeepClone(),["type"]=f["fieldType"]?.DeepClone(),
   ["required"]=f["isRequired"]?.DeepClone()??JsonValue.Create(false),["requiresPhoto"]=f["requiresPhoto"]?.DeepClone()??JsonValue.Create(false),
   ["requiresSignature"]=f["requiresSignature"]?.DeepClone()??JsonValue.Create(false),["helpText"]=f["helpText"]?.DeepClone(),["placeholder"]=f["placeholder"]?.DeepClone(),
   ["options"]=Parse(f,"options","optionsJson",new JsonArray()),["validationRules"]=Parse(f,"validationRules","validationRulesJson",new JsonObject())};
  var project=new JsonArray();foreach(var f in definition["projectFields"]!.AsArray().Where(f=>f?["isActive"]?.GetValue<bool>()!=false))project.Add(Field(f!));
  var sections=new JsonArray();foreach(var s in definition["sections"]!.AsArray())
  {
   var fields=new JsonArray();foreach(var f in s!["fields"]!.AsArray().Where(f=>f?["isActive"]?.GetValue<bool>()!=false))fields.Add(Field(f!));
   sections.Add(new JsonObject{["title"]=s["sectionName"]?.DeepClone(),["required"]=s["isRequired"]?.DeepClone()??JsonValue.Create(true),["fields"]=fields});
  }
  return new JsonObject{["revisionTemplateID"]=definition["checklistTemplateID"]!.DeepClone(),["expectedVersionID"]=definition["checklistTemplateVersionID"]!.DeepClone(),
   ["documentNumber"]=definition["templateNumber"]?.DeepClone(),["documentName"]=definition["templateName"]?.DeepClone(),["subtitle"]=definition["subtitle"]?.DeepClone(),
   ["active"]=definition["isTemplateActive"]?.DeepClone(),["revision"]="",["issueDate"]=DateTime.Today.ToString("yyyy-MM-dd"),
   ["scope"]=definition["scope"]?.DeepClone(),["acceptanceCriteria"]=definition["acceptanceCriteria"]?.DeepClone(),
   ["projectFields"]=project,["sections"]=sections,["requiredSignOffTypes"]=definition["requiredSignOffTypes"]?.DeepClone()??new JsonArray()};
 }
 private static JsonNode Parse(JsonNode field,string typedKey,string legacyKey,JsonNode fallback)
 {
  // API DTOs expose parsed JSON under options/validationRules, not repository row aliases.
  if(field[typedKey] is {} typed)return typed.DeepClone();
  var value=field[legacyKey]?.ToString();return string.IsNullOrWhiteSpace(value)?fallback:JsonNode.Parse(value)??fallback;
 }
}