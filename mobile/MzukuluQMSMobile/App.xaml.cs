using MzukuluQMSMobile.Pages;

namespace MzukuluQMSMobile;

public partial class App : Application
{
    private readonly FieldLoginPage _loginPage;

    public App(FieldLoginPage loginPage)
    {
#if WINDOWS
        WinUI.StartupTrace.Mark("MAUI Application constructor");
#endif
        InitializeComponent();
#if WINDOWS
        WinUI.StartupTrace.Mark("MAUI Application XAML initialised");
#endif

        _loginPage = loginPage;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
#if WINDOWS
        WinUI.StartupTrace.Mark("CreateWindow begin");
#endif
        var window=new Window(new NavigationPage(_loginPage));
#if WINDOWS
        window.Title="Mzukulu QC";
        window.Width=560;window.Height=850;window.MinimumWidth=360;window.MinimumHeight=480;
#endif
        window.Activated+=(_,_)=>{
#if WINDOWS
            WinUI.StartupTrace.Mark("Window Activated");
#endif
        };
        window.Created+=(_,_)=>{
#if WINDOWS
            WinUI.StartupTrace.Mark("Window Created");
            if(window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            {
                WinUI.StartupTrace.Mark("Native HWND "+WinRT.Interop.WindowNative.GetWindowHandle(native));
                native.AppWindow.Show();
                native.Activate();
                WinUI.StartupTrace.Mark("Native window shown: visible="+native.AppWindow.IsVisible+", size="+native.AppWindow.Size.Width+"x"+native.AppWindow.Size.Height);
            }
#endif
        };
        return window;
    }
}