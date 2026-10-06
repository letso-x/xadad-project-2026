namespace MzukuluQMSMobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new ContentPage
        {
            Content = new Label
            {
                Text = "Mzukulu QMS Mobile",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        });
    }
}