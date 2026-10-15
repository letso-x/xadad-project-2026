using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class PasswordRecoveryTests
{
 const string Redirect="mzukuluqms://auth/recovery";static readonly Guid Subject=Guid.NewGuid(),Code=Guid.NewGuid();
 sealed class Store:IRecoveryStateStore{public RecoveryAttempt? Attempt;public Task<RecoveryAttempt?> ReadAsync()=>Task.FromResult(Attempt);public Task WriteAsync(RecoveryAttempt value){Attempt=value;return Task.CompletedTask;}public Task ClearAsync(){Attempt=null;return Task.CompletedTask;}}
 sealed class Handler:HttpMessageHandler
 {
  public readonly Queue<Func<HttpResponseMessage>> Replies=new();public readonly List<(string Route,string Body,string? Authorization)> Requests=[];
  public void Add(object value,HttpStatusCode status=HttpStatusCode.OK)=>Replies.Enqueue(()=>new(status){Content=new StringContent(JsonSerializer.Serialize(value))});
  public void Fail()=>Replies.Enqueue(()=>throw new HttpRequestException("sensitive provider details"));
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests.Add((request.RequestUri!.PathAndQuery,request.Content is null?"":await request.Content.ReadAsStringAsync(ct),request.Headers.Authorization?.ToString()));return Replies.Dequeue()();}
 }
 static PasswordRecovery Create(Handler h,Store s)=>new(new HttpClient(h){BaseAddress=new Uri("https://auth.example.invalid/auth/v1/")},"public-test",Redirect,s);
 static object Tokens()=>new{access_token="header."+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{sub=Subject,aal="aal1",exp=DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}))).TrimEnd('=').Replace('+','-').Replace('/','_')+".signature",refresh_token="synthetic-refresh",user=new{id=Subject}};
 static Uri Link()=>new(Redirect+"?code="+Code);
 static async Task<PasswordRecovery> Accepted(Handler h,Store s)
 {h.Add(new{});h.Add(Tokens());h.Add(new{id=Subject,email="test@example.invalid"});var service=Create(h,s);await service.RequestAsync("test@example.invalid");await service.AcceptCallbackAsync(Link());return service;}
 [Theory][InlineData(200)][InlineData(400)][InlineData(404)]public async Task Account_existence_is_never_disclosed(int status)
 {var h=new Handler();h.Add(new{message="account not found private"},(HttpStatusCode)status);var s=new Store();var r=Create(h,s);Assert.Equal(PasswordRecovery.NeutralMessage,await r.RequestAsync("test@example.invalid"));Assert.Null(h.Requests[0].Authorization);Assert.False(r.CanChangePassword);Assert.DoesNotContain("private",s.Attempt!.ToString());}
 [Theory][InlineData("")][InlineData("not-email")][InlineData("Display Name <test@example.invalid>")][InlineData("a@b")]
 public async Task Invalid_email_never_sends_a_request(string email){var h=new Handler();await Assert.ThrowsAsync<ArgumentException>(()=>Create(h,new Store()).RequestAsync(email));Assert.Empty(h.Requests);}
 [Theory][InlineData(429)][InlineData(500)]public async Task Provider_availability_errors_are_safe_and_clear_verifier(int status)
 {var h=new Handler();h.Add(new{error="private payload"},(HttpStatusCode)status);var s=new Store();var e=await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(h,s).RequestAsync("test@example.invalid"));Assert.DoesNotContain("private",e.Message);Assert.Null(s.Attempt);}
 [Fact]public async Task Reset_request_network_failure_does_not_expose_exception_details()
 {var h=new Handler();h.Fail();var s=new Store();var e=await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(h,s).RequestAsync("test@example.invalid"));Assert.DoesNotContain("sensitive",e.Message);Assert.Null(s.Attempt);}
 [Fact]public async Task Pkce_hash_and_exact_redirect_are_bound_to_email_then_authenticated_user()
 {
  var h=new Handler();h.Add(new{});h.Add(Tokens());h.Add(new{id=Subject,email="test@example.invalid"});var s=new Store();var r=Create(h,s);await r.RequestAsync("test@example.invalid");var attempt=s.Attempt!;
  var body=JsonNode.Parse(h.Requests[0].Body)!;var hash=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(attempt.Verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
  Assert.Equal(hash,body["code_challenge"]!.ToString());Assert.Equal("s256",body["code_challenge_method"]!.ToString());Assert.DoesNotContain(attempt.Verifier,h.Requests[0].Body);Assert.Contains(Uri.EscapeDataString(Redirect),h.Requests[0].Route);
  await r.AcceptCallbackAsync(Link());Assert.True(r.CanChangePassword);Assert.Null(s.Attempt);Assert.Equal(attempt.Verifier,JsonNode.Parse(h.Requests[1].Body)!["code_verifier"]!.ToString());Assert.Equal("/auth/v1/user",h.Requests[2].Route);
  Assert.False((object)r is ISyncSession);
 }
 [Theory][InlineData("https://auth/recovery?code=x")][InlineData("mzukuluqms://other/recovery?code=x")][InlineData("mzukuluqms://auth/recovery#access_token=secret")][InlineData("mzukuluqms://auth/recovery?code=a&code=b")]
 public async Task Untrusted_or_implicit_callback_never_exchanges_tokens(string link)
 {var h=new Handler();var s=new Store{Attempt=new("test@example.invalid","verifier",DateTimeOffset.UtcNow.AddMinutes(5))};await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(h,s).AcceptCallbackAsync(new(link)));Assert.Empty(h.Requests);}
 [Fact]public async Task Expired_or_replayed_recovery_is_rejected()
 {var h=new Handler();var s=new Store{Attempt=new("test@example.invalid","verifier",DateTimeOffset.UtcNow.AddMinutes(-1))};var r=Create(h,s);await Assert.ThrowsAsync<InvalidOperationException>(()=>r.AcceptCallbackAsync(Link()));Assert.Empty(h.Requests);Assert.Null(s.Attempt);await Assert.ThrowsAsync<InvalidOperationException>(()=>r.AcceptCallbackAsync(Link()));}
 [Fact]public async Task Provider_identity_must_match_expected_email_and_subject()
 {var h=new Handler();h.Add(new{});h.Add(Tokens());h.Add(new{id=Guid.NewGuid(),email="different@example.invalid"});var s=new Store();var r=Create(h,s);await r.RequestAsync("test@example.invalid");await Assert.ThrowsAsync<InvalidOperationException>(()=>r.AcceptCallbackAsync(Link()));Assert.False(r.CanChangePassword);}
 [Theory][InlineData("long-enough-password","different","Passwords must match.")][InlineData("short","short","Use at least 12 characters.")]
 public void Password_validation_is_local(string password,string confirm,string expected)=>Assert.Equal(expected,PasswordRecovery.PasswordError(password,confirm));
 [Fact]public async Task Update_success_uses_authenticated_user_endpoint_clears_recovery_and_signs_out_locally()
 {var h=new Handler();var s=new Store();var r=await Accepted(h,s);h.Add(new{id=Subject});h.Add(new{});Assert.Equal(PasswordRecovery.UpdatedMessage,await r.UpdatePasswordAsync("long-enough-password","long-enough-password"));Assert.True(r.PasswordUpdated);Assert.False(r.CanChangePassword);Assert.NotNull(h.Requests[3].Authorization);Assert.EndsWith("logout?scope=local",h.Requests.Last().Route);Assert.DoesNotContain(h.Requests,q=>q.Route.Contains("factors"));}
 [Fact]public async Task Update_failure_keeps_recovery_for_retry_without_false_success()
 {var h=new Handler();var r=await Accepted(h,new Store());h.Add(new{message="secret provider detail"},HttpStatusCode.Forbidden);var e=await Assert.ThrowsAsync<InvalidOperationException>(()=>r.UpdatePasswordAsync("long-enough-password","long-enough-password"));Assert.DoesNotContain("secret",e.Message);Assert.False(r.PasswordUpdated);Assert.True(r.CanChangePassword);}
 [Fact]public async Task Update_connection_failure_is_safe()
 {var h=new Handler();var r=await Accepted(h,new Store());h.Fail();var e=await Assert.ThrowsAsync<InvalidOperationException>(()=>r.UpdatePasswordAsync("long-enough-password","long-enough-password"));Assert.DoesNotContain("sensitive",e.Message);Assert.False(r.PasswordUpdated);}
 [Fact]public async Task Password_change_requires_recovery_and_matching_passwords()
 {var h=new Handler();var r=Create(h,new Store());await Assert.ThrowsAsync<ArgumentException>(()=>r.UpdatePasswordAsync("long-enough-password","different"));await Assert.ThrowsAsync<InvalidOperationException>(()=>r.UpdatePasswordAsync("long-enough-password","long-enough-password"));Assert.Empty(h.Requests);}
 [Fact]public void Password_length_boundaries_are_enforced_without_arbitrary_complexity_rules()
 {Assert.Null(PasswordRecovery.PasswordError(new string('x',12),new string('x',12)));Assert.Null(PasswordRecovery.PasswordError(new string('x',128),new string('x',128)));Assert.NotNull(PasswordRecovery.PasswordError(new string('x',129),new string('x',129)));}
 [Fact]public async Task Failed_code_exchange_does_not_authenticate_or_keep_reusable_verifier()
 {var h=new Handler();h.Add(new{});h.Add(new{error="private"},HttpStatusCode.Forbidden);var s=new Store();var r=Create(h,s);await r.RequestAsync("test@example.invalid");var e=await Assert.ThrowsAsync<InvalidOperationException>(()=>r.AcceptCallbackAsync(Link()));Assert.Equal(PasswordRecovery.LinkError,e.Message);Assert.False(r.CanChangePassword);Assert.Null(s.Attempt);}
 [Fact]public async Task Cancel_clears_recovery_without_calling_factor_or_admin_endpoints()
 {var h=new Handler();var s=new Store();var r=await Accepted(h,s);var count=h.Requests.Count;await r.CancelAsync();Assert.False(r.CanChangePassword);Assert.Null(s.Attempt);Assert.Equal(count,h.Requests.Count);}}