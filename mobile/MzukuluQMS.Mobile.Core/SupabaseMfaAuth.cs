using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;

public sealed record AuthTokens(Guid Subject,string AccessToken,string RefreshToken,DateTimeOffset ExpiresAt,string Assurance)
{public override string ToString()=>"Authenticated session (redacted)";}
public sealed record AuthenticatorFactor(string Id,string Name);
public sealed record AuthenticatorSetup(string FactorId,string QrCode,string Secret)
{public bool HasEnrollmentUri {get;init;} public string? EnrollmentUri {get;init;} public override string ToString()=>"Authenticator setup (redacted)";}
public sealed class AuthenticatorAlreadyEnrolledException():InvalidOperationException("Your authenticator is already set up. Enter its current verification code.");

// Extends the application's existing Supabase Auth REST integration. Local JWT
// inspection routes UI only; the QMS API still cryptographically validates JWTs.
public sealed class SupabaseMfaAuth(HttpClient client,string publicKey)
{
 private readonly SemaphoreSlim gate=new(1,1);
 public AuthTokens? Tokens {get;private set;}
 public AuthenticatorSetup? Setup {get;private set;}
 public IReadOnlyList<AuthenticatorFactor> Factors {get;private set;}=[];
 public const string EnrollmentNamePrefix="Mzukulu QC setup ";
 private IReadOnlyList<string> pendingFactors=[];private string? pendingFactorId;
 private (string Factor,string Id,DateTimeOffset Expires)? challenge;
 private bool? hasVerifiedFactor;private bool requiresChallenge;
 public bool IsAssured=>!requiresChallenge&&Tokens is {Assurance:"aal2"} token&&token.ExpiresAt>DateTimeOffset.UtcNow&&hasVerifiedFactor!=false;
 public void Restore(AuthTokens tokens){Clear();Tokens=tokens;}
 public void Clear(){Tokens=null;Setup=null;Factors=[];pendingFactors=[];pendingFactorId=null;challenge=null;hasVerifiedFactor=null;requiresChallenge=false;}
 public void ClearSetupSecret()=>Setup=null;
 private async Task<T> SingleAsync<T>(Func<Task<T>> action,bool wait=false)
 {
  if(wait)await gate.WaitAsync();else if(!await gate.WaitAsync(0))throw new InvalidOperationException("Please wait for the current verification request to finish.");
  try{return await action();}finally{gate.Release();}
 }
 public Task SignInAsync(string email,string password)=>SingleAsync(async()=>{
  Clear();Tokens=ReadTokens(await SendAsync(HttpMethod.Post,"token?grant_type=password",new {email,password},false));requiresChallenge=true;
  await ReadFactorsAsync();return true;
 });
 public Task<IReadOnlyList<AuthenticatorFactor>> ListFactorsAsync()=>SingleAsync(ReadFactorsAsync);
 private async Task<IReadOnlyList<AuthenticatorFactor>> ReadFactorsAsync()
 {
  var user=await SendAsync(HttpMethod.Get,"user");
  if(!Guid.TryParse(user["id"]?.ToString(),out var id)||id!=Tokens?.Subject)throw new InvalidOperationException("Your session could not be verified. Sign in again.");
  var all=user["factors"]?.AsArray()??[];
  Factors=all.Where(f=>f?["status"]?.ToString()=="verified"&&f["factor_type"]?.ToString()=="totp").Select(f=>new AuthenticatorFactor(FactorId(f!["id"]?.ToString()),FactorName(f["friendly_name"]?.ToString()))).ToArray();
  pendingFactors=all.Where(f=>f?["status"]?.ToString()=="unverified"&&f["factor_type"]?.ToString()=="totp"&&(f["id"]?.ToString()==pendingFactorId||(f["friendly_name"]?.ToString()??"").StartsWith(EnrollmentNamePrefix,StringComparison.Ordinal))).Select(f=>FactorId(f!["id"]?.ToString())).ToArray();
  hasVerifiedFactor=Factors.Count>0;
  if(Factors.Count==0&&all.Any(f=>f?["status"]?.ToString()=="verified"))throw new InvalidOperationException("Your account uses another verification method. Contact your administrator for help signing in.");
  return Factors;
 }
 private static string FactorName(string? name)=>name?.StartsWith(EnrollmentNamePrefix,StringComparison.Ordinal)==true?(name[EnrollmentNamePrefix.Length..].StartsWith("backup ",StringComparison.Ordinal)?"Backup authenticator":"My authenticator"):string.IsNullOrWhiteSpace(name)?"Authenticator app":name;
 public Task<AuthenticatorSetup> EnrollAsync(string name,bool backup=false)=>SingleAsync(async()=>{
  if(Tokens is null)throw new InvalidOperationException("Sign in before setting up your authenticator.");
  await ReadFactorsAsync();
  if(!backup&&Factors.Count>0){Setup=null;throw new AuthenticatorAlreadyEnrolledException();}
  if(backup&&!IsAssured)throw new InvalidOperationException("Complete two-step verification before adding a backup authenticator.");
  if(Setup is not null&&pendingFactors.Contains(Setup.FactorId))return Setup;
  if(Setup is not null&&Factors.Any(f=>f.Id==Setup.FactorId)){Setup=null;throw new AuthenticatorAlreadyEnrolledException();}
  Setup=null;
  // /user cannot recover an enrollment secret. Only remove this flow's unfinished
  // factors, at AAL1: Supabase refuses AAL1 deletion if a factor became verified.
  // At AAL2 there is no conditional DELETE, so never risk deleting a verified factor.
  if(pendingFactors.Count>0&&Tokens.Assurance!="aal1")throw new InvalidOperationException("An unfinished authenticator setup needs administrator assistance before another can be added. Your verified authenticator is unchanged.");
  foreach(var pending in pendingFactors)await SendAsync(HttpMethod.Delete,$"factors/{pending}",requireAal1:true);
  if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>64)throw new ArgumentException("Enter an authenticator name of up to 64 characters.");
  var result=await SendAsync(HttpMethod.Post,"factors",new {factor_type="totp",friendly_name=name.Trim()});
  var factor=FactorId(result["id"]?.ToString());var qr=result["totp"]?["qr_code"]?.ToString();var secret=result["totp"]?["secret"]?.ToString();
  if(string.IsNullOrWhiteSpace(qr)||string.IsNullOrWhiteSpace(secret))throw new InvalidOperationException("Authenticator setup could not be loaded. Please try again.");
  pendingFactorId=factor;Setup=new(factor,qr,secret){HasEnrollmentUri=!string.IsNullOrWhiteSpace(result["totp"]?["uri"]?.ToString()),EnrollmentUri=result["totp"]?["uri"]?.ToString()};challenge=null;return Setup;
 });
 public Task ChallengeAsync(string factor)=>SingleAsync(async()=>{await CreateChallengeAsync(factor);return true;});
 private async Task CreateChallengeAsync(string factor)
 {
  factor=FactorId(factor);
  if(Setup?.FactorId!=factor&&!Factors.Any(f=>f.Id==factor))throw new InvalidOperationException("Choose an available authenticator app.");
  var result=await SendAsync(HttpMethod.Post,$"factors/{factor}/challenge",new {});
  var id=FactorId(result["id"]?.ToString());
  if(!long.TryParse(result["expires_at"]?.ToString(),out var expires))throw new InvalidOperationException("We couldn't start verification. Please try again.");
  challenge=(factor,id,DateTimeOffset.FromUnixTimeSeconds(expires));
 }
 public Task VerifyAsync(string factor,string code)=>SingleAsync(async()=>{
  if(code.Length!=6||code.Any(c=>c<'0'||c>'9'))throw new ArgumentException("Enter the 6-digit code from your authenticator app.");
  factor=FactorId(factor);
  if(challenge is null||challenge.Value.Factor!=factor||challenge.Value.Expires<=DateTimeOffset.UtcNow)await CreateChallengeAsync(factor);
  var attempt=challenge!.Value;challenge=null;
  var before=Tokens??throw new InvalidOperationException("Sign in again to continue.");
  var result=await SendAsync(HttpMethod.Post,$"factors/{factor}/verify",new {challenge_id=attempt.Id,code});
  var upgraded=ReadTokens(result);
  if(upgraded.Subject!=before.Subject||upgraded.Assurance!="aal2")throw new InvalidOperationException("Verification was not completed. Sign in again and try again.");
  Tokens=upgraded;requiresChallenge=false;Setup=null;await ReadFactorsAsync();
  if(!IsAssured)throw new InvalidOperationException("Verification was not completed. Please try again.");
  return true;
 });
 public Task<string?> GetProtectedTokenAsync()=>SingleAsync(async()=>{
  if(Tokens is null)return null;
  if(Tokens.ExpiresAt<=DateTimeOffset.UtcNow.AddMinutes(1)){await RefreshAsync();await ReadFactorsAsync();}
  return IsAssured?Tokens!.AccessToken:null;
 },true);
 private async Task RefreshAsync()
 {
  var previous=Tokens??throw new InvalidOperationException("Sign in again to continue.");
  var result=ReadTokens(await SendAsync(HttpMethod.Post,"token?grant_type=refresh_token",new {refresh_token=previous.RefreshToken},false));
  if(result.Subject!=previous.Subject){Clear();throw new InvalidOperationException("Your session changed. Sign in again to continue.");}
  Tokens=result;
 }
 public async Task LogoutAsync()
 {
  await gate.WaitAsync();
  try{if(Tokens is not null)await SendAsync(HttpMethod.Post,"logout?scope=local",new {});}
  finally{Clear();gate.Release();}
 }
 private async Task<JsonNode> SendAsync(HttpMethod method,string route,object? body=null,bool authenticated=true,bool requireAal1=false)
 {
  if(client.BaseAddress?.Scheme!="https")throw new InvalidOperationException("Secure sign-in is not configured. Contact your administrator.");
  try
  {
   if(authenticated&&Tokens is not null&&Tokens.ExpiresAt<=DateTimeOffset.UtcNow.AddSeconds(30))await RefreshAsync();
   if(requireAal1&&Tokens?.Assurance!="aal1")throw new InvalidOperationException("Authenticator setup changed. Sign in again to continue safely.");
   using var request=new HttpRequestMessage(method,route);request.Headers.Add("apikey",publicKey);
   if(authenticated){if(Tokens is null)throw new InvalidOperationException("Sign in again to continue.");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Tokens.AccessToken);}
   if(body is not null)request.Content=JsonContent.Create(body);
   using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
   if(!response.IsSuccessStatusCode)
   {
    string? error=null;try{var json=JsonNode.Parse(text);error=(json?["code"]??json?["error_code"])?.ToString();}catch(JsonException){}
    throw new InvalidOperationException(response.StatusCode==HttpStatusCode.TooManyRequests?"Too many attempts. Wait a moment before trying again.":error is "mfa_verification_failed"?"The verification code is incorrect. Try again.":error is "mfa_challenge_expired" or "mfa_challenge_not_found"?"We couldn't verify that code. Request a new code and try again.":route.StartsWith("token?grant_type=password")?"Sign-in failed. Check your email and password.":route.EndsWith("/verify")?"We couldn't verify that code. Request a new code and try again.":"We couldn't complete account verification. Please try again or contact your administrator.");
   }
   return string.IsNullOrWhiteSpace(text)?new JsonObject():JsonNode.Parse(text)??new JsonObject();
  }
  catch(Exception e) when(e is HttpRequestException or TaskCanceledException){throw new InvalidOperationException("We couldn't complete verification. Check your connection and try again.");}
  catch(Exception e) when(e is JsonException or FormatException or OverflowException){throw new InvalidOperationException("We couldn't read the verification response. Please try again.");}
 }
 private static string FactorId(string? value)=>Guid.TryParse(value,out var id)?id.ToString():throw new InvalidOperationException("Verification information is unavailable. Sign in again.");
 public static AuthTokens ReadTokens(JsonNode result)
 {
  try
  {
   var access=result["access_token"]!.GetValue<string>();var refresh=result["refresh_token"]!.GetValue<string>();var segments=access.Split('.');if(segments.Length!=3||string.IsNullOrWhiteSpace(refresh))throw new FormatException();
   var encoded=segments[1].Replace('-','+').Replace('_','/');using var claims=JsonDocument.Parse(Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'=')));
   var subject=claims.RootElement.GetProperty("sub").GetGuid();var assurance=claims.RootElement.TryGetProperty("aal",out var aal)?aal.GetString()??"":"";
   var expiry=DateTimeOffset.FromUnixTimeSeconds(claims.RootElement.GetProperty("exp").GetInt64());
   if(result["user"]?["id"] is { } id&&Guid.Parse(id.ToString())!=subject)throw new FormatException();
   return new(subject,access,refresh,expiry,assurance);
  }
  catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or NullReferenceException or KeyNotFoundException or ArgumentOutOfRangeException){throw new InvalidOperationException("Your session could not be verified. Sign in again.");}
 }
}
