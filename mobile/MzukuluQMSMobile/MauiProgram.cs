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
#if DEBUG || QMS_PERFORMANCE
        MzukuluQMS.Mobile.Core.PerformanceTrace.Sink=sample=>{
            var message=FormattableString.Invariant($"operation={sample.Operation} elapsed_ms={sample.ElapsedMilliseconds:F3} thread={(MainThread.IsMainThread?"main":"worker")}");
#if ANDROID
            Android.Util.Log.Info("QmsPerf",message);
#else
            System.Diagnostics.Trace.WriteLine(message);
#endif
        };
#endif
        using var timing=MzukuluQMS.Mobile.Core.PerformanceTrace.Measure("startup.maui");
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

        builder.Services.AddSingleton<FieldSession>();
        builder.Services.AddSingleton<IFieldMedia,FieldMedia>();
        builder.Services.AddSingleton<InspectionWorkspace>();
        builder.Services.AddTransient<FieldLoginPage>();
        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<ProjectsPage>();



        return builder.Build();
    }
}
