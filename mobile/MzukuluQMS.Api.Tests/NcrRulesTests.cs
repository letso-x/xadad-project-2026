using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class NcrRulesTests
{
 [Fact]public void Creator_and_corrector_cannot_verify_their_own_case()
 {
  var creator=Guid.NewGuid();var corrector=Guid.NewGuid();var row=new NcrRow{CreatedByUserID=creator,CorrectedByUserID=corrector,CorrectiveAction="Repaired",CorrectiveEvidenceID=2,IsBlocking=true,Revision=3};
  Assert.Throws<InvalidOperationException>(()=>NcrRules.ValidateVerification(row,creator,3));Assert.Throws<InvalidOperationException>(()=>NcrRules.ValidateVerification(row,corrector,3));NcrRules.ValidateVerification(row,Guid.NewGuid(),3);
 }
 [Fact]public void Incomplete_changed_or_closed_cases_cannot_verify()
 {
  Assert.Throws<InvalidOperationException>(()=>NcrRules.ValidateVerification(new NcrRow(),Guid.NewGuid(),0));
  Assert.Throws<InvalidOperationException>(()=>NcrRules.ValidateVerification(new NcrRow{Revision=1},Guid.NewGuid(),0));
  Assert.Throws<InvalidOperationException>(()=>NcrRules.ValidateVerification(new NcrRow{ClosedAt=DateTimeOffset.UtcNow},Guid.NewGuid(),0));
 }
 [Theory][InlineData("Approved")][InlineData("Cancelled")][InlineData("Unknown")]
 public void Protected_QC_record_cannot_be_mutated_through_NCR_actions(string status)=>Assert.Throws<InvalidOperationException>(()=>NcrRules.RequireWritableForm(status));
 [Theory][InlineData("Draft")][InlineData("InProgress")][InlineData("ReturnedForCorrection")][InlineData("Submitted")]
 public void Active_NCR_workflow_remains_available_before_approval(string status)=>NcrRules.RequireWritableForm(status);
}
