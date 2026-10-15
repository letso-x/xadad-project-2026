using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class ChecklistItemsTests
{
 [Fact] public void NewItemsHaveIndependentResultsAndOnlyConfiguredReadings()
 {
  var first=ChecklistItems.NewItem();first["label"]="Equipment securely mounted";
  var second=ChecklistItems.NewItem();second["label"]="Torque verified";
  var fields=new JsonArray(first,second);ChecklistItems.AddAssociated(fields,second);
  var torque=fields.Last()!;torque["type"]="Number";torque["label"]="Torque";torque["validationRules"]!["unit"]="Nm";
  var groups=ChecklistItems.Groups(fields);
  Assert.Equal(2,groups.Count);Assert.Single(groups[0].Fields);Assert.Equal(2,groups[1].Fields.Count);
  Assert.Equal("Number",torque["type"]!.ToString());Assert.Equal("Nm",ChecklistItems.Unit(torque));
  first["value"]="A";second["value"]="R";torque["value"]=42;
  var reopened=JsonNode.Parse(fields.ToJsonString())!.AsArray();
  Assert.Equal("A",reopened[0]!["value"]!.ToString());Assert.Equal("R",reopened[1]!["value"]!.ToString());Assert.Equal(42,reopened[2]!["value"]!.GetValue<int>());
  Assert.True(ChecklistItems.IsResultOptions(reopened[0]!["options"]!.AsArray().Select(o=>o!.ToString()).ToArray()));
 }
 [Fact] public void ReorderAndRemovalKeepAssociatedFieldsWithTheirItem()
 {
  var first=ChecklistItems.NewItem();var second=ChecklistItems.NewItem();var fields=new JsonArray(first,second);
  ChecklistItems.AddAssociated(fields,second);var reading=fields.Last()!;
  ChecklistItems.Move(fields,ChecklistItems.Groups(fields)[1],-1);
  Assert.Same(second,fields[0]);Assert.Same(reading,fields[1]);Assert.Same(first,fields[2]);
  ChecklistItems.Remove(fields,ChecklistItems.Groups(fields)[0]);Assert.Single(fields);Assert.Same(first,fields[0]);
 }
 [Fact] public void RendererGroupingUsesSnapshotMetadataAndPreservesLegacyAndOrphanFields()
 {
  var result=ChecklistItems.NewItem();var fields=new JsonArray(result);ChecklistItems.AddAssociated(fields,result);
  var snapshot=fields.DeepClone().AsArray();
  result["label"]="Later live template edit";fields.Last()!["label"]="Later unit edit";
  Assert.NotEqual(result["label"]!.ToString(),ChecklistItems.Groups(snapshot)[0].Primary["label"]!.ToString());
  var legacy=ChecklistItems.NewField();snapshot.Add(legacy);Assert.Equal(2,ChecklistItems.Groups(snapshot).Count);
  snapshot.RemoveAt(0);Assert.Equal(2,ChecklistItems.Groups(snapshot).Count);Assert.All(ChecklistItems.Groups(snapshot),g=>Assert.False(g.IsInspectionItem));
 }
 [Fact] public void DifferentTemplateDefinitionsUseSameGroupingWithoutDocumentNumbers()
 {
  var mechanical=ChecklistItems.NewItem();mechanical["label"]="Mounting";var a=new JsonArray(mechanical);
  var electrical=ChecklistItems.NewItem();electrical["label"]="Circuit identification";var b=new JsonArray(electrical);ChecklistItems.AddAssociated(b,electrical);
  b.Last()!["type"]="Reference";
  Assert.Single(ChecklistItems.Groups(a));Assert.Single(ChecklistItems.Groups(b));Assert.Equal(2,ChecklistItems.Groups(b)[0].Fields.Count);
  Assert.Equal("Reference",ChecklistItems.Groups(b)[0].Fields[1]["type"]!.ToString());
 }
}
