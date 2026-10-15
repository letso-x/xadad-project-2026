using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MzukuluQMSMobile.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            StartupTrace.Mark("WinUI constructor");
            try{Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated+=(_,args)=>ReceiveActivation(args);}catch(System.Runtime.InteropServices.COMException){StartupTrace.Mark("Protocol activation component unavailable; normal sign-in remains available.");}
            UnhandledException+=(_,e)=>StartupTrace.Mark("Unhandled "+e.Exception.GetType().Name+" HRESULT "+e.Exception.HResult.ToString("X8")+Environment.NewLine+e.Exception.StackTrace);
            this.InitializeComponent();
            StartupTrace.Mark("WinUI XAML initialised");
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            foreach(var argument in Environment.GetCommandLineArgs().Skip(1))global::MzukuluQMSMobile.Services.RecoveryLinks.Receive(argument);
            try{ReceiveActivation(Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs());}catch(System.Runtime.InteropServices.COMException){StartupTrace.Mark("Protocol activation unavailable.");}
            StartupTrace.Mark("OnLaunched begin");base.OnLaunched(args);StartupTrace.Mark("OnLaunched complete");
        }
        private static void ReceiveActivation(Microsoft.Windows.AppLifecycle.AppActivationArguments args)
        {
            if(args.Kind==Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Protocol&&args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocol)global::MzukuluQMSMobile.Services.RecoveryLinks.Receive(protocol.Uri.OriginalString);
        }
        protected override MauiApp CreateMauiApp()
        {StartupTrace.Mark("CreateMauiApp begin");var app=MauiProgram.CreateMauiApp();StartupTrace.Mark("CreateMauiApp complete");return app;}
    }

}
