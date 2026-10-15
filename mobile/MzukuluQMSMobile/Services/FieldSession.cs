using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Constants;
namespace MzukuluQMSMobile.Services;
public sealed class FieldSession : ISyncSession, ILocalKeyStore
{
 private sealed record Session(Guid Owner,string AccessToken,string RefreshToken,DateTimeOffset ExpiresAt);
 private readonly SupabaseMfaAuth auth;
 private readonly SemaphoreSlim persistence=new(1,1);
 private Guid? owner;private int generation;private string? validatedToken,persistedToken;
 public FieldSession()
 {
  var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30),MaxResponseContentBufferSize=1024*1024};
  if(Uri.TryCreate(SupabaseConstants.Url,UriKind.Absolute,out var url)&&url.Scheme=="https")client.BaseAddress=new Uri(url.ToString().TrimEnd('/')+"/auth/v1/");
  auth=new SupabaseMfaAuth(client,SupabaseConstants.AnonKey);
 }
 public Guid? Owner=>IsMfaVerified?owner:null;
 public bool IsMfaVerified=>owner.HasValue&&auth.IsAssured;
 public bool HasPendingSignIn=>auth.Tokens is not null;
 public IReadOnlyList<AuthenticatorFactor> Factors=>auth.Factors;
 public AuthenticatorSetup? Setup=>auth.Setup;
 public async Task LoginAsync(string email,string password,CancellationToken ct=default)
 {
  var attempt=++generation;owner=null;validatedToken=null;persistedToken=null;await RemoveSavedSessionAsync();auth.Clear();
  try{await auth.SignInAsync(email,password);owner=await IdentityAsync(auth.Tokens!.AccessToken,ct);validatedToken=auth.Tokens.AccessToken;if(attempt!=generation)throw new InvalidOperationException("Your session changed. Sign in again.");if(IsMfaVerified)await PersistAsync(attempt);}
  catch{if(attempt==generation){owner=null;auth.Clear();}throw;}
 }
 public async Task<bool> RestoreAsync()
 {
  if(HasPendingSignIn)return true;
  var saved=await SecureStorage.Default.GetAsync("qms-session");if(saved is null)return false;
  try
  {
   var session=JsonSerializer.Deserialize<Session>(saved);if(session is null)return false;
   var tokens=SupabaseMfaAuth.ReadTokens(new JsonObject{["access_token"]=session.AccessToken,["refresh_token"]=session.RefreshToken});
   owner=session.Owner;auth.Restore(tokens);validatedToken=tokens.AccessToken;persistedToken=tokens.AccessToken;
   if(tokens.ExpiresAt<=DateTimeOffset.UtcNow){await auth.GetProtectedTokenAsync();if(auth.Tokens!.ExpiresAt<=DateTimeOffset.UtcNow)return false;var linked=await IdentityAsync(auth.Tokens.AccessToken,default);if(linked!=session.Owner)throw new InvalidOperationException("Your session changed. Sign in again.");validatedToken=auth.Tokens.AccessToken;if(IsMfaVerified)await PersistAsync(generation);}
   return true;
  }
  catch(Exception e) when(e is JsonException or InvalidOperationException or HttpRequestException or TaskCanceledException){owner=null;auth.Clear();return false;}
 }
 public Task<IReadOnlyList<AuthenticatorFactor>> LoadFactorsAsync()=>auth.ListFactorsAsync();
 public Task<AuthenticatorSetup> EnrollAsync(bool backup=false)=>auth.EnrollAsync(SupabaseMfaAuth.EnrollmentNamePrefix+(backup?"backup ":"primary ")+Guid.NewGuid().ToString("N")[..8],backup);
 public Task ChallengeAsync(string factor)=>auth.ChallengeAsync(factor);
 public void ClearSetupSecret()=>auth.ClearSetupSecret();
 public async Task VerifyMfaAsync(string factor,string code)
 {
  var attempt=generation;var expected=owner??throw new InvalidOperationException("Sign in again to continue.");
  if(!auth.IsAssured||auth.Setup is not null)await auth.VerifyAsync(factor,code);
  var linked=await IdentityAsync(auth.Tokens!.AccessToken,default);
  if(linked!=expected||owner!=expected||attempt!=generation)throw new InvalidOperationException("Your session changed. Sign in again to continue.");
  validatedToken=auth.Tokens.AccessToken;await PersistAsync(attempt);
 }
 public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
 {
  if(!owner.HasValue)return null;var attempt=generation;var expected=owner.Value;
  var token=await auth.GetProtectedTokenAsync();
  if(token is null){await RemoveSavedSessionAsync();return null;}
  if(token!=validatedToken){if(await IdentityAsync(token,ct)!=expected)return null;validatedToken=token;}if(generation!=attempt||owner!=expected)return null;
  if(token!=persistedToken)await PersistAsync(attempt);return token;
 }
 private async Task<Guid> IdentityAsync(string token,CancellationToken ct)
 {
  using var api=new HttpClient{BaseAddress=new Uri(ApiConstants.BaseUrl),Timeout=TimeSpan.FromSeconds(30)};
  using var request=new HttpRequestMessage(HttpMethod.Get,"api/auth/me");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  using var response=await api.SendAsync(request,ct);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException(response.StatusCode switch{
   System.Net.HttpStatusCode.NotFound=>"Your sign-in account is not linked to a QMS user. Ask your administrator to link it.",
   System.Net.HttpStatusCode.Forbidden=>"Your QMS account is inactive. Ask your administrator for help.",
   System.Net.HttpStatusCode.Unauthorized=>"Your session could not be verified. Sign in again.",
   _=>"We couldn't connect to the service. Your saved work is retained."});
  using var identity=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return identity.RootElement.GetProperty("userID").GetGuid();
 }
 private async Task PersistAsync(int attempt)
 {
  await persistence.WaitAsync();
  try
  {
   if(attempt!=generation||!IsMfaVerified)throw new InvalidOperationException("Complete two-step verification to continue.");
   var tokens=auth.Tokens!;await SecureStorage.Default.SetAsync("qms-session",JsonSerializer.Serialize(new Session(owner!.Value,tokens.AccessToken,tokens.RefreshToken,tokens.ExpiresAt)));
   persistedToken=tokens.AccessToken;if(attempt!=generation){SecureStorage.Default.Remove("qms-session");throw new InvalidOperationException("Your session changed. Sign in again.");}
  }
  finally{persistence.Release();}
 }
 private async Task RemoveSavedSessionAsync(){await persistence.WaitAsync();try{SecureStorage.Default.Remove("qms-session");}finally{persistence.Release();}}
 public async Task LogoutAsync()
 {
  ++generation;owner=null;validatedToken=null;persistedToken=null;await RemoveSavedSessionAsync();
  try{await auth.LogoutAsync();}catch(InvalidOperationException){}finally{auth.Clear();await RemoveSavedSessionAsync();}
 }
 public async Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)
 {
  if(Owner!=owner)throw new InvalidOperationException("Complete sign-in to unlock your saved work.");
  var name="qms-local-key-"+owner.ToString("N");var encoded=await SecureStorage.Default.GetAsync(name);
  if(encoded is null)
  {
   if(File.Exists(Path.Combine(FileSystem.AppDataDirectory,$"qms-{owner:N}.db")))throw new InvalidOperationException("Local encryption key is unavailable. Data retained; recovery is required.");
   var key=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);encoded=Convert.ToBase64String(key);await SecureStorage.Default.SetAsync(name,encoded);
  }
  return Convert.FromBase64String(encoded);
 }
}