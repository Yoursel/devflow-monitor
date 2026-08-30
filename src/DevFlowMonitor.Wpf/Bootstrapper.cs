using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.Notification;
using DevFlowMonitor.Wpf.View;
using DevFlowMonitor.Wpf.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf;

public class Bootstrapper
{
    public static IHost Build()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(RegisterServices)
            .Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);

            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddFilter("DevFlowMonitor", LogLevel.Debug);

#if DEBUG
            builder.AddDebug();
#endif
        });
        
        services.AddHttpClient<IDevFlowApiClient, DevFlowApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<PipelineNotificationDetector>();
        services.AddSingleton<IDesktopNotificationService, WindowsDesktopNotificationService>();
        services.AddHostedService<PipelineMonitoringService>();
        
        RegisterViews(services);
        RegisterViewModels(services);
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<PipelinesListViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}
