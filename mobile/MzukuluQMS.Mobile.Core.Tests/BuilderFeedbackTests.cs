using System.Net;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class BuilderFeedbackTests
{
 [Fact] public void ApiValidationReasonIsVisibleWithoutDiscardingDraft()
 {
  Assert.Contains("required signing",ApiFailureMessage.Describe(HttpStatusCode.BadRequest,"{\"detail\":\"Configure required signing capacities explicitly.\"}"));
  Assert.Contains("Document name",ApiFailureMessage.Describe(HttpStatusCode.BadRequest,"{\"errors\":{\"DocumentName\":[\"DocumentName is required.\"]}}"));
 }
 [Theory]
 [InlineData("QCFormFieldID 123 is invalid")]
 [InlineData("UserID 12 must be active")]
 [InlineData("Npgsql error: SQLSTATE 28P01")]
 [InlineData("https://service.invalid/?password=secret")]
 [InlineData("Bearer sensitive-token")]
 [InlineData("User 8ee3e9d3-baa9-413a-b98e-790347fad04a is inactive")]
 public void Technical_or_sensitive_validation_details_are_not_displayed(string detail)
 {
  var json=new JsonObject{["detail"]=detail}.ToJsonString();
  Assert.Null(ApiFailureMessage.SafeDetail(detail));Assert.DoesNotContain(detail,ApiFailureMessage.Describe(HttpStatusCode.BadRequest,json));
 }
 [Fact] public void UnexpectedServerDetailsNeverReachUser()
 {
  Assert.DoesNotContain("secret",ApiFailureMessage.Describe(HttpStatusCode.InternalServerError,"{\"detail\":\"secret connection string\"}"));
  Assert.Contains("temporarily unavailable",ApiFailureMessage.Describe(HttpStatusCode.ServiceUnavailable,"{\"detail\":\"secret\"}"));
  Assert.Contains("rejected",ApiFailureMessage.Describe(HttpStatusCode.BadRequest,"not json"));
 }
 [Fact] public void MissingSigningCapacityHasActionablePreflightAndDoesNotMutateDraft()
 {
  var draft=Draft();draft["requiredSignOffTypes"]=new JsonArray();var before=draft.ToJsonString();
  Assert.Contains(ChecklistDraftValidation.Errors(draft),e=>e.Contains("signing capacity"));Assert.Equal(before,draft.ToJsonString());
 }
 [Fact] public void BlankLabelsAndInvalidKeysIdentifyTheActualSectionField()
 {
  var draft=Draft();var field=draft["sections"]![0]!["fields"]![0]!;field["label"]="";field["key"]="Equipment No";
  var errors=ChecklistDraftValidation.Errors(draft);Assert.Contains(errors,e=>e.Contains("field 1")&&e.Contains("lowercase"));Assert.Contains(errors,e=>e.Contains("inspection/test requirement"));
 }
 private static JsonNode Draft()=>new JsonObject{["documentNumber"]="TEST-QC-001",["documentName"]="Test Inspection",["revision"]="Rev 1",["requiredSignOffTypes"]=new JsonArray("Inspector"),["projectFields"]=new JsonArray(),["sections"]=new JsonArray(new JsonObject{["title"]="Mechanical",["fields"]=new JsonArray(ChecklistItems.NewItem())})};
}
