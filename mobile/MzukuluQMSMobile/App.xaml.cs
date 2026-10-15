using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.Pages;

namespace MzukuluQMSMobile;

public partial class App : Application
{
    private readonly FieldLoginPage _loginPage;
    private readonly PasswordRecovery recovery;private readonly FieldSession session;private readonly InspectionWorkspace workspace;

    public App(FieldLoginPage loginPage,PasswordRecovery recovery,FieldSession session,InspectionWorkspace workspace)
    {
#if WINDOWS
        WinUI.StartupTrace.Mark("MAUI Application constructor");
#endif
        InitializeComponent();
#if WINDOWS
        WinUI.StartupTrace.Mark("MAUI Application XAML initialised");
#endif

        _loginPage = loginPage;this.recovery=recovery;this.session=session;this.workspace=workspace;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
#if WINDOWS
        WinUI.StartupTrace.Mark("CreateWindow begin");
#endif
        var callback=RecoveryLinks.Take();
        var window=new Window(new NavigationPage(callback is null?_loginPage:RecoveryPage(callback)));
#if WINDOWS
        window.Title="Mzukulu QC";
        window.Width=560;window.Height=850;window.MinimumWidth=360;window.MinimumHeight=480;
#endif
        window.Activated+=(_,_)=>{ShowRecoveryIfReady();
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
    private Page RecoveryPage(Uri callback)=>new FieldPasswordRecoveryPage(recovery,callback,async updated=>{
        await workspace.DisposeAsync();await session.LogoutAsync();RecoveryLinks.Complete();
        var login=new FieldLoginPage(session,workspace,recovery);if(updated)login.SetNotice(PasswordRecovery.UpdatedMessage);
        if(Windows.FirstOrDefault() is {} window)window.Page=new NavigationPage(login);
    });
    public void ShowRecoveryIfReady()
    {
        if(Windows.FirstOrDefault() is not {} window)return;
        var callback=RecoveryLinks.Take();if(callback is not null)window.Page=new NavigationPage(RecoveryPage(callback));
    }
    protected override void OnAppLinkRequestReceived(Uri uri){RecoveryLinks.Receive(uri.OriginalString);base.OnAppLinkRequestReceived(uri);}    public void ShowSignIn(){RecoveryLinks.Complete();if(Windows.FirstOrDefault() is {} window)window.Page=new NavigationPage(new FieldLoginPage(session,workspace,recovery));}}