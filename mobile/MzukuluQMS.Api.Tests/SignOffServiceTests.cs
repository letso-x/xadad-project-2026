using Moq;
using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs.SignOffs;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;
using MzukuluQMS.Api.Exceptions;
namespace MzukuluQMS.Api.Tests;
public sealed class SignOffServiceTests
{
 private readonly Mock<IQCSignOffRepository> signs=new(); private readonly Mock<IQCFormRepository> forms=new();
 private readonly Mock<ICurrentUserService> users=new(); private readonly Mock<IQCFormContentHashService> hash=new();
 private readonly Mock<IQCWorkflow> workflow=new(); private readonly Mock<IQCOperation> operation=new();
 private readonly Guid user=Guid.NewGuid();
 private QCSignOffService Service=>new(signs.Object,forms.Object,users.Object,hash.Object,workflow.Object);
 private static CreateQCSignOffRequest Request=>new(){SignOffType="inspector",SignatureEvidenceID=22};
 public SignOffServiceTests()
 {
  workflow.Setup(w=>w.BeginAsync(101,null,"SignInspection",default)).ReturnsAsync(operation.Object);
  forms.Setup(f=>f.GetStatusAsync(101,default)).ReturnsAsync("Submitted");
  signs.Setup(s=>s.QCFormExistsAsync(101,default)).ReturnsAsync(true);
  users.Setup(u=>u.GetCurrentUserAsync(default)).ReturnsAsync(new CurrentUserDto {UserID=user,IsActive=true});
  hash.Setup(h=>h.GenerateHashAsync(101,default)).ReturnsAsync(new string('b',64));
  signs.Setup(s=>s.SignatureEvidenceIsValidAsync(101,22,user,default)).ReturnsAsync(true);
  signs.Setup(s=>s.CreateAsync(101,user,"Inspector",22,null,It.IsAny<DateTimeOffset>(),new string('b',64),default)).ReturnsAsync(new QCSignOffDto {QCSignOffID=33});
 }
 [Fact] public async Task Server_resolves_actor_hash_and_normalised_signoff_type()
 {
  var result=await Service.CreateAsync(101,Request); Assert.Equal(33,result.QCSignOffID);
  workflow.Verify(w=>w.EnsureSigningAuthorityAsync(101,"Inspector",default),Times.Once);
  operation.Verify(o=>o.CompleteAsync("QCFormSigned",101,It.IsAny<object>(),default,It.IsAny<object>()),Times.Once);
 }
 [Theory][InlineData("Draft")][InlineData("ReturnedForCorrection")][InlineData("Approved")][InlineData("Cancelled")]
 public async Task Only_submitted_can_sign(string status)
 {
  forms.Setup(f=>f.GetStatusAsync(101,default)).ReturnsAsync(status);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.CreateAsync(101,Request));
  signs.Verify(s=>s.CreateAsync(It.IsAny<long>(),It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<long>(),It.IsAny<string?>(),It.IsAny<DateTimeOffset>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
 }
 [Fact] public async Task Invalid_evidence_identity_form_type_or_reuse_prevents_signing()
 {
  signs.Setup(s=>s.SignatureEvidenceIsValidAsync(101,22,user,default)).ReturnsAsync(false);
  await Assert.ThrowsAsync<ArgumentException>(()=>Service.CreateAsync(101,Request));
 }
 [Fact] public async Task Signing_permission_denied()
 {
  workflow.Setup(w=>w.EnsureSigningAuthorityAsync(101,"Inspector",default)).ThrowsAsync(new ForbiddenException("Denied"));
  await Assert.ThrowsAsync<ForbiddenException>(()=>Service.CreateAsync(101,Request));
 }
}
