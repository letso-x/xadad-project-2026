using System.Text.Json;
namespace MzukuluQMS.Api.Services.Workflow;
public static class InspectionOutcome
{
 public static bool IsFailure(string fieldType,string? optionsJson,JsonElement value)
 {
  if(fieldType!="Dropdown"||value.ValueKind!=JsonValueKind.String)return false;
  var selected=value.GetString();if(string.Equals(selected,"Fail",StringComparison.OrdinalIgnoreCase))return true;
  if(selected!="R"||string.IsNullOrWhiteSpace(optionsJson))return false;
  using var options=JsonDocument.Parse(optionsJson);
  if(options.RootElement.ValueKind!=JsonValueKind.Array)return false;
  var choices=options.RootElement.EnumerateArray().Where(o=>o.ValueKind==JsonValueKind.String).Select(o=>o.GetString()).ToHashSet(StringComparer.Ordinal);
  return choices.SetEquals(new[]{"A","R","N/A"});
 }
}
