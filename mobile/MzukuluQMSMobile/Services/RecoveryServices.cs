using System.Text.Json;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Services;
public static class RecoveryConfiguration
{
 public const string RedirectUri="mzukuluqms://auth/recovery";
}
public sealed class SecureRecoveryState:IRecoveryStateStore
{
 private const string Key="qms-password-recovery-pkce";
 public async Task<RecoveryAttempt?> ReadAsync()
 {
  try{var raw=await SecureStorage.Default.GetAsync(Key);return raw is null?null:JsonSerializer.Deserialize<RecoveryAttempt>(raw);}
  catch(JsonException){SecureStorage.Default.Remove(Key);return null;}
 }
 public Task WriteAsync(RecoveryAttempt attempt)=>SecureStorage.Default.SetAsync(Key,JsonSerializer.Serialize(attempt));
 public Task ClearAsync(){SecureStorage.Default.Remove(Key);return Task.CompletedTask;}
}
public sealed class PlatformAuthenticatorLauncher:IAuthenticatorUriLauncher
{
 public async Task<bool> TryOpenAsync(string exactUri)
 {
#if WINDOWS
  // Optional Windows handlers are unreliable; never invoke a native component here.
  await Task.CompletedTask;return false;
#else
  try{return await Launcher.Default.TryOpenAsync(exactUri);}
  catch(Exception error) when(error is not OutOfMemoryException){return false;}
#endif
 }
}
public static class RecoveryLinks
{
 public static bool Active {get;private set;}
 private static Uri? pending;private static string? last;
 public static void Receive(string? raw)
 {
  if(raw is null||raw.Length>8192||!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||!PasswordRecovery.IsCallback(uri,RecoveryConfiguration.RedirectUri))return;
  if(Active&&raw==last)return;Active=true;last=raw;pending=uri;
  if(Application.Current is App app&&app.Windows.Count>0)MainThread.BeginInvokeOnMainThread(app.ShowRecoveryIfReady);
 }
 public static Uri? Take(){var link=pending;pending=null;return link;}
 public static void Complete(){pending=null;last=null;Active=false;}
}