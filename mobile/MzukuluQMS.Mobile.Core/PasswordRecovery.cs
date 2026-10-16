using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;

public sealed record RecoveryAttempt(string Email,string Verifier,DateTimeOffset ExpiresAt)
{public override string ToString()=>"Password recovery attempt (redacted)";}
public interface IRecoveryStateStore
{
 Task<RecoveryAttempt?> ReadAsync();Task WriteAsync(RecoveryAttempt attempt);Task ClearAsync();
}
// Isolated recovery session: never supplies an ISyncSession token or enters SupabaseMfaAuth.
public sealed class PasswordRecovery(HttpClient client,string publicKey,string redirectUri,IRecoveryStateStore state)
{
 public const string NeutralMessage="If an account exists for that email, a password reset link has been sent.";
 public const string UpdatedMessage="Password updated. You can sign in with your new password.";
 public const string LinkError="This reset link is unavailable or expired. Request a new link on this device.";
 private readonly SemaphoreSlim gate=new(1,1);private AuthTokens? recovery;
 public bool CanChangePassword=>recovery is {} token&&token.ExpiresAt>DateTimeOffset.UtcNow;
 public bool PasswordUpdated {get;private set;}
 public static bool ValidEmail(string? email)=>!string.IsNullOrWhiteSpace(email)&&email.Trim().Length<=254&&MailAddress.TryCreate(email.Trim(),out var address)&&address.Address==email.Trim()&&address.Host.Contains('.');
 public static string? PasswordError(string? password,string? confirmation)=>password!=confirmation?"Passwords must match.":password is null||password.Length<12?"Use at least 12 characters.":password.Length>128?"Use no more than 128 characters.":null;
 public static bool IsCallback(Uri uri,string redirect)=>Uri.TryCreate(redirect,UriKind.Absolute,out var target)&&uri.Scheme==target.Scheme&&uri.Host==target.Host&&uri.AbsolutePath==target.AbsolutePath&&uri.Port==target.Port&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Fragment);
 public async Task<string> RequestAsync(string email)
 {
  if(!ValidEmail(email))throw new ArgumentException("Enter a valid email address.");
  await gate.WaitAsync();
  try
  {
   recovery=null;PasswordUpdated=false;
   if(!Uri.TryCreate(redirectUri,UriKind.Absolute,out var redirect)||redirect.Scheme!="mzukuluqms"||redirect.Host!="auth"||redirect.AbsolutePath!="/recovery")throw new InvalidOperationException("Password recovery is not configured. Contact your administrator.");
   var verifier=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
   await state.WriteAsync(new(email.Trim(),verifier,DateTimeOffset.UtcNow.AddMinutes(30)));
   var challenge=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+','-').Replace('/','_');
   using var response=await SendAsync(HttpMethod.Post,"recover?redirect_to="+Uri.EscapeDataString(redirectUri),new{email=email.Trim(),code_challenge=challenge,code_challenge_method="s256"});
   if((int)response.StatusCode>=500||response.StatusCode==HttpStatusCode.TooManyRequests){await state.ClearAsync();throw new InvalidOperationException("We couldn't send the reset request. Wait a moment and try again.");}
   // Never reflect provider account-existence/error details to the caller.
   return NeutralMessage;
  }
  catch(Exception error) when(error is HttpRequestException or TaskCanceledException){await state.ClearAsync();throw new InvalidOperationException("We couldn't send the reset request. Check your connection and try again.");}
  finally{gate.Release();}
 }
 public async Task AcceptCallbackAsync(Uri uri)
 {
  await gate.WaitAsync();
  try
  {
   recovery=null;PasswordUpdated=false;
   if(!IsCallback(uri,redirectUri))throw new InvalidOperationException(LinkError);
   var query=uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Split('=',2)).ToArray();
   if(query.Length!=1||query[0][0]!="code"||query[0].Length!=2||!Guid.TryParse(Uri.UnescapeDataString(query[0][1]),out var code))throw new InvalidOperationException(LinkError);
   var attempt=await state.ReadAsync();await state.ClearAsync(); // Consume verifier before exchanging; replay never becomes a normal login.
   if(attempt is null||attempt.ExpiresAt<=DateTimeOffset.UtcNow)throw new InvalidOperationException(LinkError);
   using var response=await SendAsync(HttpMethod.Post,"token?grant_type=pkce",new{auth_code=code.ToString(),code_verifier=attempt.Verifier});
   if(!response.IsSuccessStatusCode)throw new InvalidOperationException(LinkError);
   var tokens=SupabaseMfaAuth.ReadTokens(JsonNode.Parse(await response.Content.ReadAsStringAsync())!);
   if(tokens.ExpiresAt<=DateTimeOffset.UtcNow)throw new InvalidOperationException(LinkError);
   using var user=await SendAsync(HttpMethod.Get,"user",token:tokens.AccessToken);
   if(!user.IsSuccessStatusCode)throw new InvalidOperationException(LinkError);
   var identity=JsonNode.Parse(await user.Content.ReadAsStringAsync())!;
   if(identity["id"]?.ToString()!=tokens.Subject.ToString()||!string.Equals(identity["email"]?.ToString(),attempt.Email,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(LinkError);
   recovery=tokens;
  }
  catch(Exception error) when(error is HttpRequestException or TaskCanceledException or JsonException or FormatException){recovery=null;throw new InvalidOperationException(LinkError);}
  finally{gate.Release();}
 }
 public async Task<string> UpdatePasswordAsync(string password,string confirmation)
 {
  var validation=PasswordError(password,confirmation);if(validation is not null)throw new ArgumentException(validation);
  await gate.WaitAsync();
  try
  {
   if(!CanChangePassword){recovery=null;throw new InvalidOperationException(LinkError);}
   var token=recovery!;
   using var response=await SendAsync(HttpMethod.Put,"user",new{password},token.AccessToken);
   if(!response.IsSuccessStatusCode)throw new InvalidOperationException("The password could not be updated. Try again or request a new reset link.");
   var user=JsonNode.Parse(await response.Content.ReadAsStringAsync());
   if(user?["id"]?.ToString()!=token.Subject.ToString())throw new InvalidOperationException("The password update could not be confirmed. Request a new reset link before trying again.");
   recovery=null;PasswordUpdated=true;await state.ClearAsync();
   try{using var logout=await SendAsync(HttpMethod.Post,"logout?scope=local",new{},token.AccessToken);}catch(Exception e) when(e is HttpRequestException or TaskCanceledException){}
   return UpdatedMessage;
  }
  catch(Exception error) when(error is HttpRequestException or TaskCanceledException or JsonException){throw new InvalidOperationException("The password could not be updated. Check your connection or request a new reset link.");}
  finally{gate.Release();}
 }
 public async Task CancelAsync(){await gate.WaitAsync();try{recovery=null;await state.ClearAsync();}finally{gate.Release();}}
 private async Task<HttpResponseMessage> SendAsync(HttpMethod method,string route,object? body=null,string? token=null)
 {
  if(client.BaseAddress?.Scheme!="https")throw new InvalidOperationException("Secure password recovery is not configured.");
  using var request=new HttpRequestMessage(method,route);request.Headers.Add("apikey",publicKey);
  if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(body is not null)request.Content=JsonContent.Create(body);
  return await client.SendAsync(request);
 }
}