namespace MzukuluQMS.Mobile.Core;
public interface IAuthenticatorUriLauncher {Task<bool> TryOpenAsync(string exactUri);}
public static class AuthenticatorConvenience
{
 public const string Fallback="No compatible authenticator app could be opened. Use the QR code or copy the setup key instead.";
 public static async Task<bool> OpenAsync(AuthenticatorSetup setup,IAuthenticatorUriLauncher launcher)
 {
  var exact=setup.EnrollmentUri;
  if(string.IsNullOrWhiteSpace(exact)||!Uri.TryCreate(exact,UriKind.Absolute,out var uri)||uri.Scheme!="otpauth"||uri.Host!="totp"||!string.IsNullOrEmpty(uri.UserInfo))return false;
  try{return await launcher.TryOpenAsync(exact);}catch(Exception error) when(error is not OutOfMemoryException){return false;}
 }
}