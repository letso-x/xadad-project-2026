using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.Repositories.Users;
namespace MzukuluQMS.Api.Tests;
[ApiController][Route("test/mfa")]
public sealed class MfaProbeController:ControllerBase
{
 [HttpGet("protected")][Authorize(Policy="QmsUser")]public object Protected()=>new{role=User.FindFirstValue("qms_role")};
 [HttpGet("default")][Authorize]public object Default()=>new{ok=true};
 [HttpGet("fallback")]public object Fallback()=>new{ok=true};
}
public sealed class MfaAuthorizationTests
{
 const string Issuer="https://issuer.invalid/auth/v1";
 sealed class App:WebApplicationFactory<Program>
 {
  public readonly ECDsa Key=ECDsa.Create(ECCurve.NamedCurves.nistP256);public readonly Guid Subject=Guid.NewGuid();public readonly Mock<IUserRepository> Users=new();
  public App(bool active=true,bool linked=true){Users.Setup(r=>r.GetByExternalAuthIdAsync(Subject,It.IsAny<CancellationToken>())).ReturnsAsync(linked?new CurrentUserDto{UserID=Guid.NewGuid(),ExternalAuthID=Subject,IsActive=active,Role="Inspector"}:null);}
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
   // Exercise JWT/MFA and server-owned identity mapping under the production pipeline.
   builder.UseEnvironment("Production");
   builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:QmsDatabase"]="Host=db.invalid;Database=unconnected;Username=test;Password=test;SSL Mode=VerifyFull",["SupabaseAuth:MetadataAddress"]=Issuer+"/.well-known/openid-configuration",["SupabaseAuth:Audience"]="authenticated",["SupabaseStorage:BaseUrl"]="https://storage.invalid",["SupabaseStorage:SecretKey"]="synthetic-only",["SupabaseStorage:Bucket"]="private-test"}));
   builder.ConfigureTestServices(s=>{
    s.RemoveAll<IUserRepository>();s.AddSingleton(Users.Object);s.AddControllers().AddApplicationPart(typeof(MfaProbeController).Assembly);
    s.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,o=>{
     var configuration=new OpenIdConnectConfiguration{Issuer=Issuer};configuration.SigningKeys.Add(new ECDsaSecurityKey(Key));o.ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);o.TokenValidationParameters.ClockSkew=TimeSpan.Zero;
    });
   });
  }
  public string Token(string? aal="aal2",string? issuer=null,string audience="authenticated",bool expired=false,bool wrongKey=false,bool duplicate=false)
  {
   var claims=new List<Claim>{new("sub",Subject.ToString()),new("qms_role","Admin"),new("qms_user_id",Guid.NewGuid().ToString())};if(aal!=null)claims.Add(new("aal",aal));if(duplicate)claims.Add(new("aal","aal1"));
   var key=wrongKey?ECDsa.Create(ECCurve.NamedCurves.nistP256):Key;
   var token=new JwtSecurityToken(issuer??Issuer,audience,claims,DateTime.UtcNow.AddMinutes(-10),DateTime.UtcNow.AddMinutes(expired?-1:10),new SigningCredentials(new ECDsaSecurityKey(key),SecurityAlgorithms.EcdsaSha256));return new JwtSecurityTokenHandler().WriteToken(token);
  }
  public HttpClient Client(string token){var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return client;}
 }
 [Theory][InlineData("aal1")][InlineData(null)][InlineData("unknown")]
 public async Task Password_or_missing_assurance_rejected_across_policies(string? aal)
 {await using var app=new App();using var client=app.Client(app.Token(aal));foreach(var route in new[]{"/test/mfa/protected","/test/mfa/default","/test/mfa/fallback","/api/projects","/api/auth/permissions","/api/qc-forms/101"}){using var response=await client.GetAsync(route);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}app.Users.Verify(r=>r.GetByExternalAuthIdAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);}
 [Fact]public async Task Password_session_can_only_bootstrap_active_identity()
 {await using var app=new App();using var client=app.Client(app.Token("aal1"));using var response=await client.GetAsync("/api/auth/me");Assert.Equal(HttpStatusCode.OK,response.StatusCode);}
 [Fact]public async Task Verified_session_uses_database_role_not_posted_role()
 {await using var app=new App();using var client=app.Client(app.Token());using var response=await client.GetAsync("/test/mfa/protected");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("Inspector",await response.Content.ReadAsStringAsync());Assert.DoesNotContain("Admin",await response.Content.ReadAsStringAsync());}
 [Theory][InlineData(false,true)][InlineData(true,false)]
 public async Task Inactive_or_unlinked_verified_identity_cannot_access_work(bool active,bool linked)
 {await using var app=new App(active,linked);using var client=app.Client(app.Token());using var response=await client.GetAsync("/test/mfa/protected");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);using var bootstrap=await client.GetAsync("/api/auth/me");Assert.Equal(linked?HttpStatusCode.Forbidden:HttpStatusCode.NotFound,bootstrap.StatusCode);}
 [Theory][InlineData("issuer")][InlineData("audience")][InlineData("expired")][InlineData("key")]
 public async Task Jwt_signature_issuer_audience_and_lifetime_remain_required(string invalid)
 {await using var app=new App();using var client=app.Client(app.Token(issuer:invalid=="issuer"?"https://wrong.invalid":null,audience:invalid=="audience"?"wrong":"authenticated",expired:invalid=="expired",wrongKey:invalid=="key"));using var response=await client.GetAsync("/test/mfa/protected");Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
 [Fact]public async Task Conflicting_assurance_claims_fail_closed()
 {await using var app=new App();using var client=app.Client(app.Token(duplicate:true));using var response=await client.GetAsync("/test/mfa/protected");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}
}
