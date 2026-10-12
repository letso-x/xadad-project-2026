using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Constants;
namespace MzukuluQMSMobile.Services;
public sealed class FieldSession : ISyncSession, ILocalKeyStore
{
 private sealed record Session(Guid Owner,string AccessToken,string RefreshToken,DateTimeOffset ExpiresAt);
 private Session? current;
 public Guid? Owner=>current?.Owner;
 public async Task LoginAsync(string email,string password,CancellationToken ct=default)
 {
  using var client=new HttpClient {BaseAddress=new Uri(SupabaseConstants.Url.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(30)};
  client.DefaultRequestHeaders.Add("apikey",SupabaseConstants.AnonKey);
  using var response=await client.PostAsJsonAsync("auth/v1/token?grant_type=password",new {email,password},ct);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Login failed. Check your credentials and connection.");
  using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
  var token=json.RootElement.GetProperty("access_token").GetString()!;
  using var api=new HttpClient {BaseAddress=new Uri(ApiConstants.BaseUrl),Timeout=TimeSpan.FromSeconds(30)};
  api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
  using var identityResponse=await api.GetAsync("api/auth/me",ct);
  if(!identityResponse.IsSuccessStatusCode)throw new InvalidOperationException("Your account is not linked to an active QMS user, or the API is unavailable.");
  using var identity=JsonDocument.Parse(await identityResponse.Content.ReadAsStringAsync(ct));
  var id=identity.RootElement.GetProperty("userID").GetGuid();
  current=new(id,token,json.RootElement.GetProperty("refresh_token").GetString()!,DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32()));
  await SecureStorage.Default.SetAsync("qms-session",JsonSerializer.Serialize(current));
 }
 public async Task<bool> RestoreAsync()
 {
  var saved=await SecureStorage.Default.GetAsync("qms-session");
  if(saved is null)return false;
  current=JsonSerializer.Deserialize<Session>(saved);
  // Conservative offline access: previous linked session must still be unexpired.
  return current is not null && current.ExpiresAt>DateTimeOffset.UtcNow;
 }
 public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
 {
  if(current is null)return null;
  if(current.ExpiresAt>DateTimeOffset.UtcNow.AddMinutes(1))return current.AccessToken;
  using var client=new HttpClient {BaseAddress=new Uri(SupabaseConstants.Url.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(30)};
  client.DefaultRequestHeaders.Add("apikey",SupabaseConstants.AnonKey);
  using var response=await client.PostAsJsonAsync("auth/v1/token?grant_type=refresh_token",new {refresh_token=current.RefreshToken},ct);
  if(!response.IsSuccessStatusCode)return null;
  using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
  var refreshedToken=json.RootElement.GetProperty("access_token").GetString()!;
  using var api=new HttpClient {BaseAddress=new Uri(ApiConstants.BaseUrl),Timeout=TimeSpan.FromSeconds(30)};
  api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",refreshedToken);
  using var identityResponse=await api.GetAsync("api/auth/me",ct);
  if(!identityResponse.IsSuccessStatusCode)return null;
  using var identity=JsonDocument.Parse(await identityResponse.Content.ReadAsStringAsync(ct));
  if(identity.RootElement.GetProperty("userID").GetGuid()!=current.Owner)return null;
  current=current with {AccessToken=json.RootElement.GetProperty("access_token").GetString()!,RefreshToken=json.RootElement.GetProperty("refresh_token").GetString()!,ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32())};
  await SecureStorage.Default.SetAsync("qms-session",JsonSerializer.Serialize(current));return current.AccessToken;
 }
 public void Logout(){current=null;SecureStorage.Default.Remove("qms-session");}
 public async Task<byte[]> GetKeyAsync(Guid owner,CancellationToken ct)
 {
  if(Owner!=owner)throw new InvalidOperationException("Local data is locked to its owner.");
  var name="qms-local-key-"+owner.ToString("N");var encoded=await SecureStorage.Default.GetAsync(name);
  if(encoded is null)
  {
   if(File.Exists(Path.Combine(FileSystem.AppDataDirectory,$"qms-{owner:N}.db")))throw new InvalidOperationException("Local encryption key is unavailable. Data retained; recovery is required.");
   var key=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);encoded=Convert.ToBase64String(key);await SecureStorage.Default.SetAsync(name,encoded);
  }
  return Convert.FromBase64String(encoded);
 }
}
