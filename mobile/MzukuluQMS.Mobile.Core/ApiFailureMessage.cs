using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace MzukuluQMS.Mobile.Core;
public static class ApiFailureMessage
{
 public static string? SafeDetail(string? message)
 {
  if(string.IsNullOrWhiteSpace(message))return null;
  if(Regex.IsMatch(message,@"https?://|bearer\s|sb_secret_|password\s*[:=]|connection\s*string|stack\s*trace|\bat\s+[\w.]+\(|\.cs:line|\b(?:SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+\S+\s+SET|DELETE\s+FROM)|\b(?:Npgsql|SqliteException|SQLSTATE|QCFormFieldID|QCResponseID|UserID|EvidenceID|TemplateVersionID|ChecklistTemplateID)\b|[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}",RegexOptions.IgnoreCase))return null;
  return message[..Math.Min(message.Length,2000)].Replace("DocumentName","Document name").Replace("DocumentNumber","Document number").Replace("RequiredSignOffTypes","Required signing capacities");
 }
 public static string Describe(HttpStatusCode status,string? body)
 {
  if((status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)&&!string.IsNullOrWhiteSpace(body))
  {
   try
   {
    var problem=JsonNode.Parse(body);
    if(problem?["errors"] is JsonObject errors)
    {
     var messages=errors.SelectMany(e=>e.Value is JsonArray list?list.Select(v=>v?.ToString()??""):new[]{e.Value?.ToString()??""}).Select(SafeDetail).Where(s=>s is not null).Take(8);
     var text=string.Join("\n",messages);if(text.Length>0)return "Check these values:\n"+text[..Math.Min(text.Length,2000)];
    }
    if(problem?["detail"] is JsonValue detail&&detail.TryGetValue<string>(out var message)&&SafeDetail(message) is string safe)return status==HttpStatusCode.Conflict?safe:"Check these values: "+safe;
   }
   catch(Exception e) when(e is System.Text.Json.JsonException or InvalidOperationException){ }
  }
  return status switch
  {
   HttpStatusCode.Unauthorized=>"Session expired. Sign in again; local work is retained.",
   HttpStatusCode.Forbidden=>"You do not have permission for this action or project.",
   HttpStatusCode.Conflict=>"This record changed while you were offline, or the action is not allowed in its current status. Review the latest record before continuing.",
   HttpStatusCode.BadRequest=>"The server rejected the values. Check required fields and formats.",
   HttpStatusCode.ServiceUnavailable=>"The service is temporarily unavailable. Your saved work is retained. Check the record or template register before trying an uncertain save again.",
   _=>"We couldn't complete this action. Your saved work is still available. Please try again."
  };
 }
}
