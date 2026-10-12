using MzukuluQMSMobile.Pages;

namespace MzukuluQMSMobile;

public partial class App : Application
{
    private readonly FieldLoginPage _loginPage;

    public App(FieldLoginPage loginPage)
    {
        InitializeComponent();

        _loginPage = loginPage;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        return new Window(new NavigationPage(_loginPage));
    }
}