using System.Security.Claims;
using Moq;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.Repositories.Users;
using MzukuluQMS.Api.Security;
namespace MzukuluQMS.Api.Tests;
public sealed class IdentityMappingTests
{
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Inactive_or_unlinked_users_cannot_keep_posted_qms_claims(bool inactive)
 {
  var subject=Guid.NewGuid(); var repo=new Mock<IUserRepository>();
  repo.Setup(r=>r.GetByExternalAuthIdAsync(subject,default)).ReturnsAsync(inactive?new CurrentUserDto{IsActive=false}:null);
  var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub",subject.ToString()),new Claim("qms_user_id",Guid.NewGuid().ToString()),new Claim("qms_role","Admin")},"test"));
  await new QmsClaimsTransformation(repo.Object).TransformAsync(principal);
  Assert.Null(principal.FindFirst("qms_user_id")); Assert.Null(principal.FindFirst("qms_role"));
 }
 [Fact] public async Task Server_mapping_replaces_supplied_role_and_user()
 {
  var subject=Guid.NewGuid();var id=Guid.NewGuid();var repo=new Mock<IUserRepository>();
  repo.Setup(r=>r.GetByExternalAuthIdAsync(subject,default)).ReturnsAsync(new CurrentUserDto{UserID=id,Role="Inspector",IsActive=true});
  var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub",subject.ToString()),new Claim("qms_role","Admin")},"test"));
  var result=await new QmsClaimsTransformation(repo.Object).TransformAsync(principal);
  Assert.Equal("Inspector",result.FindFirst("qms_role")!.Value);Assert.Equal(id.ToString(),result.FindFirst("qms_user_id")!.Value);
 }
}
