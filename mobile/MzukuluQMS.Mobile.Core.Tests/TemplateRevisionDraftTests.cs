using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class TemplateRevisionDraftTests
{
 private static JsonNode Definition()=>JsonNode.Parse("""
 {"checklistTemplateVersionID":10,"checklistTemplateID":3,"templateNumber":"TEST-1","templateName":"Inspection","subtitle":null,"isTemplateActive":true,"revision":"Rev 1","scope":"Safe scope","acceptanceCriteria":"All accepted","requiredSignOffTypes":["Inspector","Client"],"projectFields":[{"fieldKey":"area","fieldLabel":"Area","fieldType":"Text","isRequired":true,"isActive":true}],"sections":[{"sectionName":"Inspection","isRequired":true,"fields":[{"fieldKey":"result","fieldLabel":"Inspect cable","fieldType":"Dropdown","isRequired":true,"requiresPhoto":true,"requiresSignature":false,"isActive":true,"optionsJson":"[\"A\",\"R\",\"N/A\"]","validationRulesJson":"{\"inspectionItem\":{\"key\":\"result\",\"role\":\"Result\"}}"},{"fieldKey":"retired","fieldLabel":"Old field","fieldType":"Text","isActive":false}]}]}
 """)!;
 [Fact] public void Revision_preserves_business_rules_but_requires_a_new_label()
 {
  var original=Definition();var before=original.ToJsonString();var draft=TemplateRevisionDraft.FromDefinition(original);
  Assert.Equal("",draft["revision"]!.ToString());Assert.Equal("10",draft["expectedVersionID"]!.ToString());
  Assert.Equal(2,draft["requiredSignOffTypes"]!.AsArray().Count);Assert.Single(draft["sections"]![0]!["fields"]!.AsArray());
  var field=draft["sections"]![0]!["fields"]![0]!;Assert.True(field["requiresPhoto"]!.GetValue<bool>());
  Assert.Equal("Result",field["validationRules"]!["inspectionItem"]!["role"]!.ToString());Assert.Equal(3,field["options"]!.AsArray().Count);
  field["label"]="New label";Assert.Equal(before,original.ToJsonString());
 }
 [Fact] public void Malformed_rules_are_retained_on_source_and_rejected_instead_of_silently_lost()
 {
  var source=Definition();source["sections"]![0]!["fields"]![0]!["validationRulesJson"]="{broken";
  Assert.ThrowsAny<System.Text.Json.JsonException>(()=>TemplateRevisionDraft.FromDefinition(source));
  Assert.Equal("{broken",source["sections"]![0]!["fields"]![0]!["validationRulesJson"]!.ToString());
 }
 [Fact] public void Anonymous_unpublished_definition_cannot_be_revised()=>Assert.Throws<ArgumentException>(()=>TemplateRevisionDraft.FromDefinition(new JsonObject()));
}
