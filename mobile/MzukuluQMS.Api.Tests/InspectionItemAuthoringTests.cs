using System.Text.Json;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class InspectionItemAuthoringTests
{
 private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
 private static TemplateFieldInput Result(string key)=>new(key,"Inspection requirement","Dropdown",true,false,false,null,null,Json("[\"A\",\"R\",\"N/A\"]"),Json(JsonSerializer.Serialize(new{inspectionItem=new{key,role="Result"}})));
 private static TemplateFieldInput Reading(string key,string parent)=>new(key,"Torque","Number",false,false,false,null,null,null,Json(JsonSerializer.Serialize(new{min=0,unit="Nm",inspectionItem=new{key=parent,role="Additional"}})));
 private static CreateChecklistTemplateRequest Template(params TemplateSectionInput[] sections)=>new("TEST-QC-001","Test Inspection",null,"Rev 1",new(2026,10,14),true,"Test scope","All items must be verified.",[],sections,["Inspector"]);
 [Fact] public void FourItemsTwoSectionsAndOneTorqueReadingAreValid()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("Mechanical Inspection",true,[Result("mounted"),Result("labelled"),Result("torque"),Reading("torque_nm","torque")]),new TemplateSectionInput("Close-Out",true,[Result("clean")])));
 [Fact] public void AdditionalFieldCannotLinkAcrossSections()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("A",true,[Result("first")]),new TemplateSectionInput("B",true,[Result("second"),Reading("reading","first")]))));
 [Fact] public void ResultNeedsMatchingOwnKey()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("A",true,[Result("first") with {Key="other"}]))));
 [Fact] public void ResultCannotBeOneFormWideTextValue()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("A",true,[Result("first") with {Type="Text"}]))));
 [Fact] public void ResultOptionsMustBeAcceptedRejectedNotApplicable()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("A",true,[Result("first") with {Options=Json("[\"Yes\",\"No\"]")}]))));
 [Fact] public void MalformedRelationIsRejected()=>Assert.Throws<ArgumentException>(()=>TemplateAuthoring.Validate(Template(new TemplateSectionInput("A",true,[Result("first") with {ValidationRules=Json("{\"inspectionItem\":[]} ")}]))));
}
