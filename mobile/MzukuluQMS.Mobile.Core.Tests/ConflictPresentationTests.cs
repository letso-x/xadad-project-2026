using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class ConflictPresentationTests
{
 [Fact]public void Conflict_compares_labelled_answers_without_mutating_either_version()
 {
  var request=JsonSerializer.Serialize(new SyncRequest("PUT","api/qc-forms/10/responses/7","{\"value\":\"A\"}"));
  const string server="{\"sections\":[{\"fields\":[{\"qcResponseID\":7,\"fieldLabel\":\"Cable condition\",\"value\":\"R\"}]}]}";
  var conflict=new SyncConflict(Guid.NewGuid(),request,server,"Changed");var result=ConflictPresentation.Describe(conflict);
  Assert.Equal("Cable condition",result.Label);Assert.Equal("A",result.SavedValue);Assert.Equal("R",result.CentralValue);Assert.Equal(server,conflict.ServerJson);
 }
 [Fact]public void Workflow_conflict_displays_business_action_and_central_status()
 {
  var conflict=new SyncConflict(Guid.NewGuid(),JsonSerializer.Serialize(new SyncRequest("POST","api/qc-forms/10/submit")),"{\"status\":\"Approved\"}","Changed");
  var result=ConflictPresentation.Describe(conflict);Assert.Equal("Submit record",result.SavedValue);Assert.Equal("Approved",result.CentralValue);
 }
 [Fact]public void Invalid_payload_does_not_dump_raw_json_or_throw()
 {var result=ConflictPresentation.Describe(new(Guid.NewGuid(),"broken payload","{bad","Changed"));Assert.Equal("Saved change",result.Label);Assert.DoesNotContain("{bad",result.CentralValue);}
 [Fact]public void Schema_setup_conflict_preserves_safe_administrator_guidance()
 {const string message="Cable registers need administrator setup. QC records and saved work are unaffected.";Assert.Equal(message,ApiFailureMessage.Describe(System.Net.HttpStatusCode.Conflict,JsonSerializer.Serialize(new{detail=message})));}
 [Theory][InlineData("connection string: Password=synthetic-only")][InlineData("SELECT secret FROM internal_table")]
 public void Conflict_diagnostics_do_not_escape_sanitizer(string detail)
 {Assert.DoesNotContain(detail,ApiFailureMessage.Describe(System.Net.HttpStatusCode.Conflict,JsonSerializer.Serialize(new{detail})));}
}
