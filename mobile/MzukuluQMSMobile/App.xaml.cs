using MzukuluQMSMobile.Pages;

namespace MzukuluQMSMobile;

public partial class App : Application
{
    private readonly ProjectsPage _projectsPage;

    public App(ProjectsPage projectsPage)
    {
        InitializeComponent();

        _projectsPage = projectsPage;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        return new Window(_projectsPage);
    }
}