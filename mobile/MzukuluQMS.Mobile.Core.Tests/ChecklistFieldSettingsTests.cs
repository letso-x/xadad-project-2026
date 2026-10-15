using System.Globalization;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class ChecklistFieldSettingsTests
{
 [Fact]public void Limits_preserve_item_links_units_and_unrelated_settings_without_mutating_the_original()
 {
  var original=JsonNode.Parse("{\"min\":0,\"max\":10,\"unit\":\"Nm\",\"inspectionItem\":{\"key\":\"item_a\",\"role\":\"Additional\"},\"custom\":{\"retained\":true}}")!.AsObject();var before=original.ToJsonString();
  var result=ChecklistFieldSettings.Limits(original,new Dictionary<string,string?>{["min"]="2,5",["max"]=""},CultureInfo.GetCultureInfo("de-DE"));
  Assert.Equal(2.5m,result["min"]!.GetValue<decimal>());Assert.Null(result["max"]);Assert.True(JsonNode.DeepEquals(original["inspectionItem"],result["inspectionItem"]));Assert.True(JsonNode.DeepEquals(original["custom"],result["custom"]));Assert.Equal("Nm",result["unit"]!.ToString());Assert.Equal(before,original.ToJsonString());
 }
 [Theory][InlineData("min","11")][InlineData("minLength","-1")][InlineData("maxLength","2.5")][InlineData("max","unfinished-")]
 public void Invalid_limits_do_not_destroy_the_previous_configuration(string key,string value)
 {
  var original=JsonNode.Parse("{\"min\":0,\"max\":10}")!.AsObject();var before=original.ToJsonString();
  Assert.Throws<ArgumentException>(()=>ChecklistFieldSettings.Limits(original,new Dictionary<string,string?>{[key]=value},CultureInfo.InvariantCulture));Assert.Equal(before,original.ToJsonString());
 }
 [Fact]public void Reference_repair_retains_original_draft_values_and_relinks_associated_readings()
 {
  var item=ChecklistItems.NewItem();var fields=new JsonArray(item);ChecklistItems.AddAssociated(fields,item);var reading=fields[1]!;
  var readingKey=reading["key"]!.ToString();item["key"]="invalid key";item["validationRules"]!["inspectionItem"]!["key"]="invalid key";reading["validationRules"]!["inspectionItem"]!["key"]="invalid key";reading["label"]="Torque";reading["validationInput"]="unfinished rules";
  var draft=new JsonObject{["projectFields"]=new JsonArray(),["sections"]=new JsonArray(new JsonObject{["fields"]=fields})};var before=draft.ToJsonString();
  Assert.True(ChecklistFieldSettings.NeedsReferenceRepair(draft));var repaired=ChecklistFieldSettings.RepairReferences(draft);var repairedFields=repaired["sections"]![0]!["fields"]!.AsArray();
  Assert.False(ChecklistFieldSettings.NeedsReferenceRepair(repaired));Assert.Equal("invalid key",repairedFields[0]!["previousKeys"]![0]!.ToString());Assert.Single(ChecklistItems.Groups(repairedFields));Assert.Equal("Torque",repairedFields[1]!["label"]!.ToString());Assert.Equal("unfinished rules",repairedFields[1]!["validationInput"]!.ToString());Assert.Equal(before,draft.ToJsonString());Assert.Equal(readingKey,repairedFields[1]!["key"]!.ToString());
 }
 [Fact]public void Ambiguous_duplicate_result_repair_fails_without_changing_the_draft()
 {
  var item=ChecklistItems.NewItem();var duplicate=item.DeepClone();var draft=new JsonObject{["projectFields"]=new JsonArray(),["sections"]=new JsonArray(new JsonObject{["fields"]=new JsonArray(item,duplicate)})};var before=draft.ToJsonString();
  Assert.Throws<ArgumentException>(()=>ChecklistFieldSettings.RepairReferences(draft));Assert.Equal(before,draft.ToJsonString());
 }
}
