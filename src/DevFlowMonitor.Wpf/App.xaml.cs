using System.Windows;
using DevFlowMonitor.Wpf.View;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevFlowMonitor.Wpf;

public partial class App : System.Windows.Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Bootstrapper.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        _host.Services.GetRequiredService<ITrayIconService>().Initialize(mainWindow);
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _host.Services.GetRequiredService<ITrayIconService>().Dispose();
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
