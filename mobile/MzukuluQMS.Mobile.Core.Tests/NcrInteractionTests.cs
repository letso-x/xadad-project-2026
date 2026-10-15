using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;

public sealed class NcrInteractionTests
{
 private static JsonNode Case(Guid creator,Guid corrector)=>new JsonObject{
  ["ncrID"]=21,["createdByUserID"]=creator.ToString(),["correctedByUserID"]=corrector.ToString(),
  ["correctiveAction"]="Repair inspected",["correctiveEvidenceID"]=8,["isBlocking"]=true,["revision"]=2};
 private static JsonNode Draft()=>new JsonObject{["severity"]="Major",["classification"]="Installation",["disposition"]="Rework",["correctiveAction"]="Repaired and photographed",["correctiveEvidenceID"]=8,["isBlocking"]=true,["expectedRevision"]=2};
 [Fact]public void Creator_and_corrector_cannot_verify_but_independent_actor_can()
 {var creator=Guid.NewGuid();var corrector=Guid.NewGuid();var n=Case(creator,corrector);Assert.NotNull(NcrInteraction.VerificationProblem(n,creator));Assert.NotNull(NcrInteraction.VerificationProblem(n,corrector));Assert.Null(NcrInteraction.VerificationProblem(n,Guid.NewGuid()));}
 [Theory][InlineData("createdByUserID")][InlineData("correctedByUserID")][InlineData("correctiveAction")][InlineData("correctiveEvidenceID")][InlineData("isBlocking")]
 public void Incomplete_cases_cannot_be_verified(string missing)
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());n[missing]=null;Assert.NotNull(NcrInteraction.VerificationProblem(n,Guid.NewGuid()));}
 [Fact]public void Failed_verification_stays_open_and_can_be_independently_reverified()
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());n["verificationPassed"]=false;Assert.False(NcrInteraction.IsClosed(n));Assert.Equal("Verification failed — open",NcrInteraction.Status(n));Assert.Null(NcrInteraction.VerificationProblem(n,Guid.NewGuid()));}
 [Fact]public void Closed_and_historical_cases_remain_read_only()
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());n["closedAt"]="2026-10-15T10:00:00Z";Assert.Equal("Closed",NcrInteraction.Status(n));Assert.Throws<InvalidOperationException>(()=>NcrInteraction.ValidateCorrection(n,Draft()));n["closedAt"]=null;n["isLegacyOnly"]=true;Assert.NotNull(NcrInteraction.VerificationProblem(n,Guid.NewGuid()));Assert.Throws<InvalidOperationException>(()=>NcrInteraction.ValidateCorrection(n,Draft()));}
 [Theory][InlineData("severity")][InlineData("classification")][InlineData("disposition")][InlineData("correctiveAction")][InlineData("isBlocking")][InlineData("correctiveEvidenceID")][InlineData("expectedRevision")]
 public void Correction_requires_explicit_classification_and_uploaded_evidence(string missing)
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());var draft=Draft();draft[missing]=null;Assert.Throws<ArgumentException>(()=>NcrInteraction.ValidateCorrection(n,draft));}
 [Fact]public void Verification_comments_are_required_and_bounded()
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());Assert.Throws<ArgumentException>(()=>NcrInteraction.ValidateVerification(n,Guid.NewGuid()," "));Assert.Throws<ArgumentException>(()=>NcrInteraction.ValidateVerification(n,Guid.NewGuid(),new string('x',2001)));NcrInteraction.ValidateVerification(n,Guid.NewGuid(),"Rework independently checked.");}
 [Fact]public void Valid_correction_does_not_mutate_original_case_or_draft()
 {var n=Case(Guid.NewGuid(),Guid.NewGuid());var d=Draft();var before=n.ToJsonString();var draft=d.ToJsonString();NcrInteraction.ValidateCorrection(n,d);Assert.Equal(before,n.ToJsonString());Assert.Equal(draft,d.ToJsonString());}
 [Fact]public void Approved_cancelled_or_unknown_form_never_offers_NCR_mutation()
 {Assert.False(NcrInteraction.CanChangeForForm("Approved"));Assert.False(NcrInteraction.CanChangeForForm("Cancelled"));Assert.False(NcrInteraction.CanChangeForForm(null));Assert.True(NcrInteraction.CanChangeForForm("Submitted"));Assert.True(NcrInteraction.CanChangeForForm("ReturnedForCorrection"));}
}
