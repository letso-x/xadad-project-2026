using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class SupabaseMfaTests
{
 static readonly Guid Subject=Guid.NewGuid();static readonly string Factor=Guid.NewGuid().ToString();
 static string Token(string aal,Guid? subject=null,int seconds=3600)=>"e30."+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{sub=subject??Subject,aal,exp=DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeSeconds()}))).TrimEnd('=').Replace('+','-').Replace('/','_')+".test";
 static object Session(string aal,Guid? subject=null,int seconds=3600)=>new{access_token=Token(aal,subject,seconds),refresh_token="synthetic-refresh",user=new{id=subject??Subject}};
 static object User(bool verified)=>new{id=Subject,factors=verified?new[]{new{id=Factor,factor_type="totp",status="verified",friendly_name="Test authenticator"}}:[]};
 static object Challenge(int seconds=300)=>new{id=Guid.NewGuid(),expires_at=DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeSeconds()};
 sealed class Handler:HttpMessageHandler
 {
  public Task? HoldEnrollment;public Queue<(HttpStatusCode Status,object Body)> Responses=[];public List<string> Routes=[];public List<string?> Bearers=[];
  public void Add(object body,HttpStatusCode status=HttpStatusCode.OK)=>Responses.Enqueue((status,body));
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {Routes.Add(request.Method+" "+request.RequestUri!.PathAndQuery);Bearers.Add(request.Headers.Authorization?.Parameter);if(HoldEnrollment is not null&&request.Method==HttpMethod.Post&&request.RequestUri.AbsolutePath=="/auth/v1/factors")await HoldEnrollment;var result=Responses.Dequeue();return new HttpResponseMessage(result.Status){Content=new StringContent(JsonSerializer.Serialize(result.Body),Encoding.UTF8,"application/json")};}
 }
 static SupabaseMfaAuth Create(Handler h)=>new(new HttpClient(h){BaseAddress=new Uri("https://auth.invalid/auth/v1/")},"synthetic-public-key");
 [Fact]public async Task Enrollment_requires_verified_factor_and_replaces_password_token()
 {
  var h=new Handler();h.Add(Session("aal1"));h.Add(User(false));h.Add(User(false));h.Add(new{id=Factor,totp=new{qr_code="<svg/>",secret="synthetic-setup"}});h.Add(Challenge());h.Add(Session("aal2"));h.Add(User(true));var auth=Create(h);
  await auth.SignInAsync("test@example.invalid","synthetic");Assert.False(auth.IsAssured);Assert.Null(await auth.GetProtectedTokenAsync());
  var setup=await auth.EnrollAsync("Test");Assert.False(auth.IsAssured);Assert.DoesNotContain("synthetic-setup",setup.ToString());
  await auth.ChallengeAsync(setup.FactorId);await auth.VerifyAsync(setup.FactorId,"123456");Assert.True(auth.IsAssured);Assert.Null(auth.Setup);Assert.Equal(auth.Tokens!.AccessToken,await auth.GetProtectedTokenAsync());Assert.Equal(auth.Tokens.AccessToken,h.Bearers.Last());Assert.Empty(h.Responses);
 }
 [Theory][InlineData("aal1")][InlineData("aal2")]
 public async Task Returning_password_login_requires_fresh_challenge_even_if_provider_claims_aal2(string assurance)
 {
  var h=new Handler();h.Add(Session(assurance));h.Add(User(true));h.Add(Challenge());h.Add(Session("aal2"));h.Add(User(true));var auth=Create(h);
  await auth.SignInAsync("test@example.invalid","synthetic");Assert.Null(await auth.GetProtectedTokenAsync());await auth.VerifyAsync(Factor,"123456");Assert.True(auth.IsAssured);
 }
 [Fact]public async Task Wrong_code_is_safe_and_retry_uses_new_challenge()
 {
  var h=new Handler();h.Add(Session("aal1"));h.Add(User(true));h.Add(Challenge());h.Add(new{code="mfa_verification_failed",message="private-provider-detail"},HttpStatusCode.UnprocessableEntity);h.Add(Challenge());h.Add(Session("aal2"));h.Add(User(true));var auth=Create(h);
  await auth.SignInAsync("test@example.invalid","synthetic");var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.VerifyAsync(Factor,"123456"));Assert.Contains("incorrect",error.Message);Assert.DoesNotContain("private",error.Message);Assert.False(auth.IsAssured);await auth.VerifyAsync(Factor,"654321");Assert.True(auth.IsAssured);Assert.Equal(2,h.Routes.Count(r=>r.EndsWith("/challenge")));
 }
 [Theory][InlineData("12345")][InlineData("1234567")][InlineData("abcdef")][InlineData("１２３４５６")]
 public async Task Invalid_code_never_reaches_provider(string code)
 {var h=new Handler();var auth=Create(h);await Assert.ThrowsAsync<ArgumentException>(()=>auth.VerifyAsync(Factor,code));Assert.Empty(h.Routes);}
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Downgraded_or_swapped_verify_response_never_unlocks(bool swapped)
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(true));h.Add(Challenge());h.Add(Session(swapped?"aal2":"aal1",swapped?Guid.NewGuid():Subject));var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.VerifyAsync(Factor,"123456"));Assert.False(auth.IsAssured);}
 [Fact]public async Task Expired_challenge_is_replaced()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(true));h.Add(Challenge(-10));h.Add(Challenge());h.Add(Session("aal2"));h.Add(User(true));var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await auth.ChallengeAsync(Factor);await auth.VerifyAsync(Factor,"123456");Assert.True(auth.IsAssured);Assert.Equal(2,h.Routes.Count(r=>r.EndsWith("/challenge")));}
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Refresh_downgrade_or_identity_change_blocks_protected_access(bool swapped)
 {var h=new Handler();h.Add(Session(swapped?"aal2":"aal1",swapped?Guid.NewGuid():Subject));if(!swapped)h.Add(User(true));var auth=Create(h);auth.Restore(SupabaseMfaAuth.ReadTokens(JsonSerializer.SerializeToNode(Session("aal2",seconds:-10))!));if(swapped)await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.GetProtectedTokenAsync());else Assert.Null(await auth.GetProtectedTokenAsync());Assert.False(auth.IsAssured);}
 [Fact]public async Task Concurrent_refresh_is_serial_and_logout_clears_even_on_provider_failure()
 {var h=new Handler();h.Add(Session("aal2"));h.Add(User(true));h.Add(new{message="private"},HttpStatusCode.ServiceUnavailable);var auth=Create(h);auth.Restore(SupabaseMfaAuth.ReadTokens(JsonSerializer.SerializeToNode(Session("aal2",seconds:-10))!));var tokens=await Task.WhenAll(auth.GetProtectedTokenAsync(),auth.GetProtectedTokenAsync());Assert.All(tokens,t=>Assert.NotNull(t));Assert.Single(h.Routes,r=>r.Contains("grant_type=refresh_token"));await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.LogoutAsync());Assert.Null(auth.Tokens);Assert.Null(auth.Setup);Assert.False(auth.IsAssured);Assert.EndsWith("logout?scope=local",h.Routes.Last());}
 [Fact]public void Tokens_are_redacted()=>Assert.DoesNotContain("synthetic-refresh",SupabaseMfaAuth.ReadTokens(JsonSerializer.SerializeToNode(Session("aal2"))!).ToString());
 [Theory][InlineData("<svg xmlns='http://www.w3.org/2000/svg'><path d='M0 0h1v1z'/></svg>")][InlineData("data:image/svg+xml,%3Csvg%2F%3E")]
 public void Safe_qr_renders_locally(string svg)=>Assert.Contains("default-src 'none'",AuthenticatorQr.Html(svg));
 [Theory][InlineData("<svg><script>alert(1)</script></svg>")][InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///private'>]><svg>&x;</svg>")][InlineData("<svg onload='run()'/>")][InlineData("<svg><path fill='url(https://external.invalid)'/></svg>")]
 public void Active_or_external_qr_is_rejected(string svg)=>Assert.Throws<InvalidOperationException>(()=>AuthenticatorQr.Html(svg));
 static object PendingUser(bool owned=true)=>new{id=Subject,factors=new[]{new{id=Factor,factor_type="totp",status="unverified",friendly_name=owned?SupabaseMfaAuth.EnrollmentNamePrefix+"primary synthetic":"Other application"}}};
 static object Enrollment()=>new{id=Factor,totp=new{qr_code=ProviderSvg,secret="synthetic-setup",uri="otpauth://totp/synthetic?secret=SYNTHETIC"}};
 const string ProviderSvg="<?xml version=\"1.0\"?><!-- Generated by SVGo --><svg width=\"87\" height=\"87\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><rect x=\"12\" y=\"12\" width=\"3\" height=\"3\" style=\"fill:black;stroke:none\"/><rect x=\"15\" y=\"12\" width=\"3\" height=\"3\" style=\"fill:white;stroke:none\"/></svg>";
 [Fact]public void Supabase_svg_paint_styles_render_without_arbitrary_css()
 {var html=AuthenticatorQr.Html(ProviderSvg);Assert.Contains("fill=\"black\"",html);Assert.Contains("stroke=\"none\"",html);Assert.Contains("viewBox=\"0 0 87 87\"",html);Assert.DoesNotContain("style=\"fill",html);Assert.Equal(html,AuthenticatorQr.Html("data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(ProviderSvg))));Assert.Equal(html,AuthenticatorQr.Html("data:image/svg+xml;utf-8,"+Uri.EscapeDataString(ProviderSvg)));}
 [Theory][InlineData("fill:url(https://external.invalid)")][InlineData("fill:black;position:fixed")][InlineData("stroke:expression(run())")]
 public void Unsupported_svg_styles_are_rejected(string style)=>Assert.Throws<InvalidOperationException>(()=>AuthenticatorQr.Html("<svg><rect style='"+style+"'/></svg>"));
 [Fact]public async Task Anonymous_enrollment_never_sends_a_request()
 {var h=new Handler();await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(h).EnrollAsync("Test"));Assert.Empty(h.Routes);}
 [Fact]public async Task Verified_factor_is_rechecked_before_enrollment_and_never_deleted()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(false));h.Add(User(true));var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await Assert.ThrowsAsync<AuthenticatorAlreadyEnrolledException>(()=>auth.EnrollAsync("Test"));Assert.Single(auth.Factors);Assert.DoesNotContain(h.Routes,r=>r.StartsWith("DELETE")||r=="POST /auth/v1/factors");}
 [Fact]public async Task Repeated_setup_reuses_in_memory_enrollment_and_manual_key()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(false));h.Add(User(false));h.Add(Enrollment());h.Add(PendingUser());var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");var first=await auth.EnrollAsync("Test");var second=await auth.EnrollAsync("Test");Assert.Same(first,second);Assert.True(second.HasEnrollmentUri);Assert.Equal("otpauth://totp/synthetic?secret=SYNTHETIC",second.EnrollmentUri);Assert.Equal("synthetic-setup",second.Secret);Assert.Single(h.Routes,r=>r=="POST /auth/v1/factors");Assert.DoesNotContain(h.Routes,r=>r.StartsWith("DELETE"));}
 [Fact]public async Task Cleared_secret_restarts_only_this_flows_unverified_factor_at_aal1()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(false));h.Add(User(false));h.Add(Enrollment());h.Add(PendingUser());h.Add(new{});h.Add(Enrollment());var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await auth.EnrollAsync("Test");auth.ClearSetupSecret();await auth.EnrollAsync("Test");Assert.Single(h.Routes,r=>r=="DELETE /auth/v1/factors/"+Factor);Assert.Equal(2,h.Routes.Count(r=>r=="POST /auth/v1/factors"));}
 [Fact]public async Task Provider_denial_of_stale_cleanup_prevents_new_enrollment()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(PendingUser());h.Add(PendingUser());h.Add(new{code="insufficient_aal"},HttpStatusCode.Forbidden);var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.EnrollAsync("Test"));Assert.DoesNotContain(h.Routes,r=>r=="POST /auth/v1/factors");}
 [Fact]public async Task Other_applications_unverified_factors_are_preserved()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(PendingUser(false));h.Add(PendingUser(false));h.Add(Enrollment());var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await auth.EnrollAsync("Test");Assert.DoesNotContain(h.Routes,r=>r.StartsWith("DELETE"));}
 [Fact]public async Task Backup_setup_cannot_delete_a_potentially_verified_factor_at_aal2()
 {var h=new Handler();h.Add(PendingUser());var auth=Create(h);auth.Restore(SupabaseMfaAuth.ReadTokens(JsonSerializer.SerializeToNode(Session("aal2"))!));await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.EnrollAsync("Test",backup:true));Assert.DoesNotContain(h.Routes,r=>r.StartsWith("DELETE")||r=="POST /auth/v1/factors");}
 [Fact]public async Task Successful_enrollment_then_password_login_detects_verified_factor_without_reenrollment()
 {var h=new Handler();h.Add(Session("aal1"));h.Add(User(false));h.Add(User(false));h.Add(Enrollment());h.Add(Challenge());h.Add(Session("aal2"));h.Add(User(true));h.Add(new{});h.Add(Session("aal1"));h.Add(User(true));var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");await auth.EnrollAsync("Test");await auth.VerifyAsync(Factor,"123456");await auth.LogoutAsync();await auth.SignInAsync("test@example.invalid","synthetic");Assert.Single(auth.Factors);Assert.Null(auth.Setup);Assert.False(auth.IsAssured);Assert.Single(h.Routes,r=>r=="POST /auth/v1/factors");}
 [Fact]public async Task Concurrent_setup_clicks_cannot_create_two_factors()
 {var released=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var h=new Handler{HoldEnrollment=released.Task};h.Add(Session("aal1"));h.Add(User(false));h.Add(User(false));h.Add(Enrollment());var auth=Create(h);await auth.SignInAsync("test@example.invalid","synthetic");var first=auth.EnrollAsync("Test");try{await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.EnrollAsync("Test"));}finally{released.SetResult();}await first;Assert.Single(h.Routes,r=>r=="POST /auth/v1/factors");}
}