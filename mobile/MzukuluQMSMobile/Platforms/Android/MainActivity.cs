using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MzukuluQMSMobile
{
    [IntentFilter(new[]{Android.Content.Intent.ActionView},Categories=new[]{Android.Content.Intent.CategoryDefault,Android.Content.Intent.CategoryBrowsable},DataScheme="mzukuluqms",DataHost="auth",DataPath="/recovery")]
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState){global::MzukuluQMSMobile.Services.RecoveryLinks.Receive(Intent?.DataString);base.OnCreate(savedInstanceState);}
        protected override void OnNewIntent(Android.Content.Intent? intent){base.OnNewIntent(intent);global::MzukuluQMSMobile.Services.RecoveryLinks.Receive(intent?.DataString);}
    }
}
