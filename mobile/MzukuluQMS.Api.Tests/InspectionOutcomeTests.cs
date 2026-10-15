using System.Text.Json;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class InspectionOutcomeTests
{
 [Theory][InlineData("Dropdown","[\"Pass\",\"Fail\",\"N/A\"]","Fail",true)][InlineData("Dropdown","[\"Pass\",\"Fail\",\"N/A\"]","Pass",false)][InlineData("Dropdown","[\"A\",\"R\",\"N/A\"]","R",true)][InlineData("Dropdown","[\"L\",\"R\"]","R",false)][InlineData("Text",null,"Fail",false)]
 public void Only_explicit_inspection_outcomes_create_ncrs(string type,string? options,string value,bool expected)
 {using var json=JsonDocument.Parse(JsonSerializer.Serialize(value));Assert.Equal(expected,InspectionOutcome.IsFailure(type,options,json.RootElement));}
}
