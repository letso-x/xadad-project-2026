using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Tests;
public sealed class SignatureRulesTests
{
 private static readonly DateTime Submitted=DateTime.UtcNow;
 private static SignatureCandidate Valid=>new(){SignOffType="Inspector",HashVersion=2,ContentHash=new string('a',64),SignedAt=Submitted.AddSeconds(2),EvidenceCreatedAt=Submitted.AddSeconds(1),QCFormFieldID=51,IsActive=true,HasAuthority=true,HasProjectAccess=true,HasValidEvidence=true};
 [Fact]public void Changed_content_rejects_old_signature_without_modifying_history()
 {
  var old=Valid;var oldHash=old.ContentHash;
  Assert.Throws<InvalidOperationException>(()=>SignatureRules.RequireCurrent(new[]{"Inspector"},Array.Empty<long>(),new[]{old},new string('b',64),Submitted));
  Assert.Equal(oldHash,old.ContentHash);Assert.Equal(2,old.HashVersion);
  var fresh=Valid with {ContentHash=new string('b',64)};
  SignatureRules.RequireCurrent(new[]{"Inspector"},Array.Empty<long>(),new[]{old,fresh},new string('b',64),Submitted);
 }
 [Fact]public void Legacy_hash_cannot_be_relabelled_current()=>Assert.False(SignatureRules.IsCurrent(Valid with {HashVersion=1},Valid.ContentHash,Submitted));
 [Fact]public void No_content_change_resubmit_still_requires_fresh_consent_and_evidence()
 {Assert.False(SignatureRules.IsCurrent(Valid,Valid.ContentHash,Submitted.AddMinutes(1)));Assert.False(SignatureRules.IsCurrent(Valid with {SignedAt=Submitted.AddMinutes(2)},Valid.ContentHash,Submitted.AddMinutes(1)));}
 [Theory][InlineData("inactive")][InlineData("authority")][InlineData("project")][InlineData("evidence")]
 public void Invalid_signer_or_evidence_never_authorises_approval(string missing)
 {var signature=missing switch {"inactive"=>Valid with {IsActive=false},"authority"=>Valid with {HasAuthority=false},"project"=>Valid with {HasProjectAccess=false},_=>Valid with {HasValidEvidence=false}};Assert.False(SignatureRules.IsCurrent(signature,Valid.ContentHash,Submitted));}
 [Fact]public void Both_required_types_and_field_bindings_must_exist()
 {
  var inspector=Valid;
  Assert.Throws<InvalidOperationException>(()=>SignatureRules.RequireCurrent(new[]{"Inspector","Client"},new long[]{51},new[]{inspector},inspector.ContentHash,Submitted));
  Assert.Throws<InvalidOperationException>(()=>SignatureRules.RequireCurrent(new[]{"Inspector"},new long[]{52},new[]{inspector},inspector.ContentHash,Submitted));
  SignatureRules.RequireCurrent(new[]{"Inspector","Client"},new long[]{51,52},new[]{inspector,Valid with {SignOffType="Client",QCFormFieldID=52}},inspector.ContentHash,Submitted);
 }
 [Fact]public void Missing_policy_is_denied()=>Assert.Throws<InvalidOperationException>(()=>SignatureRules.RequireCurrent(Array.Empty<string>(),Array.Empty<long>(),new[]{Valid},Valid.ContentHash,Submitted));
}
