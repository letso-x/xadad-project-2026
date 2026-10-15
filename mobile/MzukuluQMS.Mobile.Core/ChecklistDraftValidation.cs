using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace MzukuluQMS.Mobile.Core;
public static class ChecklistDraftValidation
{
 public static IReadOnlyList<string> Errors(JsonNode draft)
 {
  var errors=new List<string>();
  foreach(var (key,label) in new[]{("documentNumber","Document number"),("documentName","Document name"),("revision","Revision")})
  {var value=draft[key]?.ToString();if(string.IsNullOrWhiteSpace(value)||value.Length>200)errors.Add(label+" is required (maximum 200 characters).");}
  if(draft["requiredSignOffTypes"] is not JsonArray capacities||capacities.Count==0)errors.Add("Choose at least one required signing capacity below, such as Inspector.");
  var sections=draft["sections"] as JsonArray??new();if(sections.Count==0)errors.Add("Add at least one section containing an inspection item.");
  var keys=new HashSet<string>(StringComparer.Ordinal);
  void Field(JsonNode? field,string location)
  {
   if(field is null)return;var key=field["key"]?.ToString()??"";
   if(!Regex.IsMatch(key,"^[a-z][a-z0-9_]{0,63}$"))errors.Add(location+": field key must start with a lowercase letter and use only lowercase letters, numbers and underscores (maximum 64 characters).");
   else if(!keys.Add(key))errors.Add(location+": duplicate field key "+key+". Give each field its own key.");
   if(string.IsNullOrWhiteSpace(field["label"]?.ToString()))errors.Add(location+": enter a field label or inspection/test requirement.");
   if(field["validationError"]?.GetValue<bool>()==true)errors.Add(location+": correct the field limits before saving.");
   else if(field["validationRules"] is JsonObject rules){try{ChecklistFieldSettings.Limits(rules,new Dictionary<string,string?>(),System.Globalization.CultureInfo.InvariantCulture);}catch(ArgumentException e){errors.Add(location+": "+e.Message);}}
  }
  var index=0;foreach(var field in draft["projectFields"] as JsonArray??new())Field(field,"Project field "+(++index));
  index=0;foreach(var section in sections)
  {
   var label=section?["title"]?.ToString();var location=string.IsNullOrWhiteSpace(label)?"Section "+(++index):"Section "+(++index)+" ("+label+")";
   var fields=section?["fields"] as JsonArray??new();if(fields.Count==0)errors.Add(location+": add at least one inspection item or field.");
   var number=0;foreach(var field in fields)Field(field,location+", field "+(++number));
  }
  return errors;
 }
}
