using MzukuluQMSMobile.Constants;
using MzukuluQMSMobile.Data.Remote;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMSMobile.Pages;

namespace MzukuluQMSMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });


        builder.Services.AddHttpClient<IQmsApiClient, QmsApiClient>(
            client =>
            {
                client.BaseAddress = new Uri(ApiConstants.BaseUrl);
            });

        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<ProjectsPage>();



        return builder.Build();
    }
}