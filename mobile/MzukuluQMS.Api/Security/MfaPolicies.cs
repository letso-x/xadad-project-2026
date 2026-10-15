using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace MzukuluQMS.Api.Security;
public static class MfaPolicies
{
 public static bool IsAssured(ClaimsPrincipal user)=>user.Identity?.IsAuthenticated==true&&user.FindAll("aal").Select(c=>c.Value).SequenceEqual(new[]{"aal2"});
 public static void Configure(AuthorizationOptions options)
 {
  var protectedAccess=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim("aal","aal2").RequireAssertion(c=>IsAssured(c.User)).Build();
  options.DefaultPolicy=protectedAccess;options.FallbackPolicy=protectedAccess;
  options.AddPolicy("AuthBootstrap",p=>p.RequireAuthenticatedUser());
  foreach(var permission in new[]{"ReadProjects","ManageProjects","ReadClients","ReadTemplates","ManageTemplates","ReadReports","DatabaseHealth"})
   options.AddPolicy(permission,p=>p.RequireAuthenticatedUser().RequireClaim("aal","aal2").RequireAssertion(c=>IsAssured(c.User)).AddRequirements(new PermissionRequirement(permission)));
  options.AddPolicy("QmsUser",p=>p.RequireAuthenticatedUser().RequireClaim("aal","aal2").RequireAssertion(c=>IsAssured(c.User)).RequireClaim("qms_user_id").RequireClaim("qms_role"));
  options.AddPolicy("QmsAdmin",p=>p.RequireAuthenticatedUser().RequireClaim("aal","aal2").RequireAssertion(c=>IsAssured(c.User)).RequireClaim("qms_user_id").RequireClaim("qms_role","Admin"));
 }
}
