using System.Globalization;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;
public static class FormInput
{
 public static bool Editable(string status)=>status is "Draft" or "InProgress" or "ReturnedForCorrection";
 public static JsonNode? Parse(string type,string? text)
 {
  if(string.IsNullOrWhiteSpace(text))return null;
  return type switch {
   "Number"=>JsonValue.Create(long.Parse(text,NumberStyles.Integer,CultureInfo.CurrentCulture)),
   "Decimal"=>JsonValue.Create(decimal.Parse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.CurrentCulture)),
   "Date"=>JsonValue.Create(DateOnly.Parse(text,CultureInfo.InvariantCulture).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)),
   "DateTime"=>JsonValue.Create(DateTimeOffset.Parse(text,CultureInfo.InvariantCulture).ToString("O")),
   _=>JsonValue.Create(text)};
 }
 public static void Validate(JsonNode field,JsonNode? value)
 {
  var error=ValidationError(field,value);
  if(error is not null)throw new ArgumentException(error);
 }
 public static string? ValidationError(JsonNode field,JsonNode? value)
 {
  if(Missing(value))return field["isRequired"]?.GetValue<bool>()==true?"A value is required for this field.":null;
  var type=field["fieldType"]?.ToString();var rules=field["validationRules"];
  if(type is "Number" or "Decimal")
  {
   if(!decimal.TryParse(value!.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||type=="Number"&&decimal.Truncate(number)!=number)return "Enter a valid "+(type=="Number"?"whole number.":"decimal value.");
   if(rules?["min"] is not null&&number<rules["min"]!.GetValue<decimal>())return "Value must be at least "+rules["min"];
   if(rules?["max"] is not null&&number>rules["max"]!.GetValue<decimal>())return "Value must be at most "+rules["max"];
  }
  if(type is "Text" or "LongText" or "Reference")
  {
   var text=value!.GetValue<string>();if(rules?["minLength"] is not null&&text.Length<rules["minLength"]!.GetValue<int>())return "Text is shorter than the configured minimum.";
   if(rules?["maxLength"] is not null&&text.Length>rules["maxLength"]!.GetValue<int>())return "Text exceeds the configured maximum length.";
  }
  if(type is "Dropdown" or "MultiSelect")
  {
   var options=field["options"]?.AsArray().Select(o=>o!.GetValue<string>()).ToHashSet(StringComparer.Ordinal)??[];
   var selected=type=="MultiSelect"?value!.AsArray().Select(o=>o!.GetValue<string>()):new[]{value!.GetValue<string>()};
   if(selected.Any(s=>!options.Contains(s)))return "Choose only configured options.";
  }
  return null;
 }
 public static bool Missing(JsonNode? value)=>value is null || value is JsonArray a && a.Count==0 || value is JsonValue j && j.TryGetValue<string>(out var s)&&string.IsNullOrWhiteSpace(s);
 public static IReadOnlyList<string> RequiredErrors(JsonNode form,IReadOnlySet<long> photoFields)
 {
  var errors=new List<string>();
  foreach(var field in form["projectFields"]?.AsArray()??[])if(field is not null && field["isRequired"]?.GetValue<bool>()==true && Missing(field["value"]))errors.Add(field["fieldLabel"]+" is required.");
  foreach(var section in form["sections"]?.AsArray()??[])foreach(var field in section?["fields"]?.AsArray()??[])
  {
   if(field is null)continue;var type=field["fieldType"]?.GetValue<string>();var required=field["isRequired"]?.GetValue<bool>()==true;
   if((field["requiresPhoto"]?.GetValue<bool>()==true || type=="Photo"&&required)&&!photoFields.Contains(field["qcFormFieldID"]!.GetValue<long>()))errors.Add(field["fieldLabel"]+" needs a saved photo.");
   if(required && type is not ("Photo" or "Signature")&&Missing(field["value"]))errors.Add(field["fieldLabel"]+" is required.");
  }
  return errors;
 }
}
