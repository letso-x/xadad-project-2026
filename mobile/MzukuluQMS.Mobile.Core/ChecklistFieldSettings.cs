using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace MzukuluQMS.Mobile.Core;
public static class ChecklistFieldSettings
{
 public static JsonObject Limits(JsonObject existing,IReadOnlyDictionary<string,string?> input,CultureInfo culture)
 {
  var result=(JsonObject)existing.DeepClone();
  foreach(var (key,label,length) in new[]{("min","Minimum value",false),("max","Maximum value",false),("minLength","Minimum characters",true),("maxLength","Maximum characters",true)})
  {
   if(!input.TryGetValue(key,out var text))continue;
   if(string.IsNullOrWhiteSpace(text)){result.Remove(key);continue;}
   if(length)
   {
    if(!int.TryParse(text,NumberStyles.Integer,culture,out var count)||count<0)throw new ArgumentException(label+" must be a whole number of zero or more.");
    result[key]=count;
   }
   else
   {
    if(!decimal.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,culture,out var number))throw new ArgumentException(label+" must be a valid number.");
    result[key]=number;
   }
  }
  decimal Read(string key)
  {
   if(!decimal.TryParse(result[key]!.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var value))throw new ArgumentException("Review the saved field limits.");
   if(key is "minLength" or "maxLength"&&(value<0||value>int.MaxValue||decimal.Truncate(value)!=value))throw new ArgumentException("Character limits must be whole numbers of zero or more.");
   return value;
  }
  foreach(var key in new[]{"min","max","minLength","maxLength"})if(result[key] is not null)Read(key);
  if(result["min"] is not null&&result["max"] is not null&&Read("min")>Read("max"))throw new ArgumentException("Minimum value cannot exceed maximum value.");
  if(result["minLength"] is not null&&result["maxLength"] is not null&&Read("minLength")>Read("maxLength"))throw new ArgumentException("Minimum characters cannot exceed maximum characters.");
  return result;
 }
 private static IEnumerable<JsonNode> Fields(JsonNode draft)=>(draft["projectFields"]?.AsArray()??[]).Concat((draft["sections"]?.AsArray()??[]).SelectMany(s=>s?["fields"]?.AsArray()??[])).Where(f=>f is not null).Select(f=>f!);
 public static bool NeedsReferenceRepair(JsonNode draft)
 {
  var seen=new HashSet<string>(StringComparer.Ordinal);
  return Fields(draft).Any(f=>!Regex.IsMatch(f["key"]?.ToString()??"","^[a-z][a-z0-9_]{0,63}$")||!seen.Add(f["key"]!.ToString()));
 }
 public static JsonNode RepairReferences(JsonNode original)
 {
  var draft=original.DeepClone();var seen=new HashSet<string>(StringComparer.Ordinal);
  var sections=draft["sections"]?.AsArray()??[];
  foreach(var section in sections)
  {
   var fields=section?["fields"]?.AsArray()??[];
   if(fields.Where(f=>f is not null&&ChecklistItems.Role(f)=="Result").GroupBy(f=>f!["key"]?.ToString()).Any(g=>g.Count()>1))throw new ArgumentException("Some repeated inspection items need administrator review. Your draft is retained.");
  }
  foreach(var field in Fields(draft))
  {
   var old=field["key"]?.ToString()??"";
   if(Regex.IsMatch(old,"^[a-z][a-z0-9_]{0,63}$")&&seen.Add(old))continue;
   var replacement="field_"+Guid.NewGuid().ToString("N");seen.Add(replacement);
   if(field["previousKeys"] is not JsonArray history){history=new JsonArray();field["previousKeys"]=history;}history.Add(old);
   field["key"]=replacement;
   if(ChecklistItems.Role(field)=="Result")
   {
    var section=sections.FirstOrDefault(s=>(s?["fields"]?.AsArray()??[]).Any(f=>ReferenceEquals(f,field)));
    foreach(var related in section?["fields"]?.AsArray()??[])if(related is not null&&ChecklistItems.ItemKey(related)==old)related["validationRules"]!["inspectionItem"]!["key"]=replacement;
   }
  }
  return draft;
 }
}
