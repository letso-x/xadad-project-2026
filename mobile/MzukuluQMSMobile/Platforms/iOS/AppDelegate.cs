using Foundation;

namespace MzukuluQMSMobile
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
        public override bool OpenUrl(UIKit.UIApplication application,NSUrl url,NSDictionary options){if(Uri.TryCreate(url.AbsoluteString,UriKind.Absolute,out var uri)&&MzukuluQMS.Mobile.Core.PasswordRecovery.IsCallback(uri,global::MzukuluQMSMobile.Services.RecoveryConfiguration.RedirectUri)){global::MzukuluQMSMobile.Services.RecoveryLinks.Receive(url.AbsoluteString);return true;}return base.OpenUrl(application,url,options);}
    }
}
