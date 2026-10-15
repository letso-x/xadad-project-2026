using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;
public sealed record ConflictComparison(string Label,string SavedValue,string CentralValue);
public static class ConflictPresentation
{
 public static ConflictComparison Describe(SyncConflict conflict,JsonNode? localForm=null)
 {
  try
  {
   var request=JsonSerializer.Deserialize<SyncRequest>(conflict.LocalJson)??throw new JsonException();
   var server=JsonNode.Parse(conflict.ServerJson)??throw new JsonException();
   if(localForm?["serverIdMap"] is not null)server=OfflineFormIdentity.Localize(server,localForm);
   var parts=request.Route.Split('/');var body=string.IsNullOrWhiteSpace(request.Body)?null:JsonNode.Parse(request.Body);
   if(parts.Length>=5&&parts[3] is "responses" or "project-fields")
   {
    var fields=parts[3]=="project-fields"?server["projectFields"]!.AsArray():new JsonArray(server["sections"]!.AsArray().SelectMany(s=>s!["fields"]!.AsArray()).Select(f=>f!.DeepClone()).ToArray());
    var idKey=parts[3]=="project-fields"?"qcFormProjectFieldID":"qcResponseID";
    var field=fields.FirstOrDefault(f=>f?[idKey]?.ToString()==parts[4]);
    return new(field?["fieldLabel"]?.ToString()??"Inspection answer",Value(body?["value"]),field is null?"Answer unavailable":Value(field["value"]));
   }
   if(parts.LastOrDefault()=="remarks")return new("Remarks",Value(body?["remarks"]),Value(server["remarks"]));
   return new("Workflow action",Action(parts.LastOrDefault()),BusinessText.Status(server["status"]?.ToString()??"Unavailable"));
  }
  catch(Exception e) when(e is JsonException or InvalidOperationException or ArgumentException or NullReferenceException)
  {return new("Saved change","Retained on this device","Current details unavailable. Ask an authorised reviewer to compare the record.");}
 }
 private static string Action(string? route)=>route switch{"submit"=>"Submit record","approve"=>"Approve record","return-for-correction"=>"Return for correction","sign-offs"=>"Add signature","evidence"=>"Upload evidence",_=>"Saved change awaiting review"};
 private static string Value(JsonNode? value)
 {
  if(value is null)return "Not recorded";
  if(value is JsonObject)return "Structured answer retained";
  if(value is JsonArray array)return string.Join(", ",array.Take(20).Select(Value));
  if(value is JsonValue json&&json.TryGetValue<bool>(out var flag))return flag?"Yes":"No";
  var text=value.ToString();return text.Length>1000?text[..1000]+"…":text;
 }
}
