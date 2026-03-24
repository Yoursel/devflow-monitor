using System.Reflection;
using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<INavigationService, NavigationService>();
        RegisterViews(services);
        RegisterViewModels(services);
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var viewModelTypes = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("ViewModel"));

        foreach (var type in viewModelTypes)
            services.AddSingleton(type);
    }
}

