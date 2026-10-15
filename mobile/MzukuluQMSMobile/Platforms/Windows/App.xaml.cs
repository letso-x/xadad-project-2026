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
            UnhandledException+=(_,e)=>StartupTrace.Mark("Unhandled "+e.Exception.GetType().Name+" HRESULT "+e.Exception.HResult);
            this.InitializeComponent();
            StartupTrace.Mark("WinUI XAML initialised");
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {StartupTrace.Mark("OnLaunched begin");base.OnLaunched(args);StartupTrace.Mark("OnLaunched complete");}
        protected override MauiApp CreateMauiApp()
        {StartupTrace.Mark("CreateMauiApp begin");var app=MauiProgram.CreateMauiApp();StartupTrace.Mark("CreateMauiApp complete");return app;}
    }

}
