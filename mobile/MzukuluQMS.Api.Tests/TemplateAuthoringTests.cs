using System.Text.Json;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class TemplateAuthoringTests
{
 private static TemplateFieldInput Field(string key,string type="Text",string? options=null)=>new(key,key,type,true,false,false,null,null,options is null?null:JsonDocument.Parse(options).RootElement.Clone(),null);
 private static CreateChecklistTemplateRequest Valid()=>new("QCS-TEST","Equipment inspection",null,"Rev 1",new DateOnly(2026,10,13),true,"Scope","Acceptance",[],[new("Inspection details",true,[Field("equipment_number"),Field("inspection_result","Dropdown","[\"Pass\",\"Fail\",\"N/A\"]")])],["Inspector"]);
 [Fact]public void Two_field_regression_definition_is_accepted()=>TemplateAuthoring.Validate(Valid());
 [Fact]public void Headings_without_actual_fields_are_rejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {Sections=[new("Empty",true,[])]}));
 [Fact]public void Duplicate_stable_keys_across_project_and_checklist_are_rejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {ProjectFields=[Field("equipment_number")]}));
 [Fact]public void Signing_policy_must_be_explicit()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {RequiredSignOffTypes=[]}));
 [Theory][InlineData("Admin")][InlineData("Inspector,Client")]
 public void Roles_are_not_signing_capacities(string type)=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {RequiredSignOffTypes=[type]}));
 [Fact]public void Project_evidence_fields_are_rejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {ProjectFields=[Field("project_photo","Photo")]}));
 [Fact]public void Unsupported_types_are_rejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {ProjectFields=[Field("unknown","Arbitrary")]}));
 [Fact]public void Duplicate_choice_options_are_rejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Valid() with {ProjectFields=[Field("duplicate","Dropdown","[\"A\",\"A\"]")]}));
}
