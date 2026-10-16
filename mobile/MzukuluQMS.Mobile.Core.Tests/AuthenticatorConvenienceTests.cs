namespace MzukuluQMS.Mobile.Core.Tests;
public sealed class AuthenticatorConvenienceTests
{
 const string Exact="otpauth://totp/Example%3Atest%40example.invalid?secret=SYNTHETIC&issuer=Example%20QC";
 sealed class Launcher(bool success=true,bool fail=false):IAuthenticatorUriLauncher
 {public string? Received;public Task<bool> TryOpenAsync(string exact){Received=exact;if(fail)throw new System.Runtime.InteropServices.COMException("No installed components were detected.");return Task.FromResult(success);}}
 [Fact]public async Task Exact_provider_uri_is_forwarded_without_changing_qr_or_manual_key()
 {var setup=new AuthenticatorSetup("factor","provider-svg","provider-secret"){EnrollmentUri=Exact,HasEnrollmentUri=true};var launcher=new Launcher();Assert.True(await AuthenticatorConvenience.OpenAsync(setup,launcher));Assert.Equal(Exact,launcher.Received);Assert.Equal("provider-svg",setup.QrCode);Assert.Equal("provider-secret",setup.Secret);Assert.DoesNotContain("SYNTHETIC",setup.ToString());}
 [Theory][InlineData(null)][InlineData("")][InlineData("https://example.invalid/secret")]
 public async Task Missing_or_wrong_scheme_uses_fallback_without_launcher(string? uri)
 {var launcher=new Launcher();Assert.False(await AuthenticatorConvenience.OpenAsync(new("factor","qr","key"){EnrollmentUri=uri},launcher));Assert.Null(launcher.Received);}
 [Theory][InlineData(false)][InlineData(true)]public async Task No_handler_or_com_failure_is_not_a_crash(bool fail)
 {var launcher=new Launcher(false,fail);Assert.False(await AuthenticatorConvenience.OpenAsync(new("factor","qr","key"){EnrollmentUri=Exact},launcher));Assert.Equal(Exact,launcher.Received);}
}