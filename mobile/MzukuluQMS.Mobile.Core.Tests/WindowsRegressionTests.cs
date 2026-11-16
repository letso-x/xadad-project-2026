using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;

public sealed class WindowsRegressionTests
{
 [Theory]
 [InlineData("Text")][InlineData("LongText")][InlineData("Number")][InlineData("Decimal")][InlineData("Reference")][InlineData("Dropdown")]
 public void Empty_required_edit_validation_returns_information_without_first_chance_exceptions(string type)
 {
  var field=new JsonObject{["fieldType"]=type,["isRequired"]=true};
  var thread=Environment.CurrentManagedThreadId;var exceptions=0;
  void Observe(object? sender,FirstChanceExceptionEventArgs args){if(Environment.CurrentManagedThreadId==thread)exceptions++;}
  AppDomain.CurrentDomain.FirstChanceException+=Observe;
  try{for(var i=0;i<50;i++)Assert.Equal("A value is required for this field.",FormInput.ValidationError(field,null));}
  finally{AppDomain.CurrentDomain.FirstChanceException-=Observe;}
  Assert.Equal(0,exceptions);
  // Strict mutation boundary remains available; missing fields have not become optional.
  Assert.Throws<ArgumentException>(()=>FormInput.Validate(field,null));
 }
 [Fact]public void Inline_errors_clear_after_valid_input_and_still_enforce_limits()
 {
  var field=JsonNode.Parse("{\"fieldType\":\"Decimal\",\"isRequired\":true,\"validationRules\":{\"min\":0,\"max\":10}}")!;
  Assert.NotNull(FormInput.ValidationError(field,null));Assert.Null(FormInput.ValidationError(field,JsonValue.Create(0)));
  Assert.NotNull(FormInput.ValidationError(field,JsonValue.Create(11)));Assert.Null(FormInput.ValidationError(field,JsonValue.Create(10)));
 }
 [Theory]
 [InlineData("A","R")][InlineData("Pass","Fail")][InlineData("Accepted","Rejected")]
 public void Inspection_outcome_options_use_button_semantics_independently_of_labels(string accepted,string rejected)
 {
  // The renderer uses this predicate, including legacy fields without inspection-item metadata.
  var result=new[]{accepted,rejected,"N/A"};
  Assert.True(ChecklistItems.IsResultOptions(result));Assert.True(ChecklistItems.IsResultOptions(result.Reverse().ToArray()));
  Assert.False(ChecklistItems.IsResultOptions(new[]{"Low","Medium","High"}));
  Assert.False(ChecklistItems.IsResultOptions(new[]{accepted,rejected,"N/A","Other"}));
  var field=new JsonObject{["fieldLabel"]="Any legacy requirement",["fieldType"]="Dropdown",["options"]=new JsonArray(result.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray())};
  foreach(var answer in result)Assert.Null(FormInput.ValidationError(field,JsonValue.Create(answer)));
  Assert.NotNull(FormInput.ValidationError(field,JsonValue.Create("Unknown")));
 }
}