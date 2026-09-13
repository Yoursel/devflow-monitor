using System.Windows;
using DevFlowMonitor.Wpf.View;
using DevFlowMonitor.Wpf.Service;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkiaSharp;

namespace DevFlowMonitor.Wpf;

public partial class App : System.Windows.Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureCharts();

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

    private static void ConfigureCharts()
    {
        var fontResource = GetResourceStream(
            new Uri("Style/Fonts/VCROSDMono[NolivantNTEdit]-Regular.ttf", UriKind.Relative));
        using var fontStream = fontResource?.Stream;
        var typeface = fontStream is null ? SKTypeface.Default : SKTypeface.FromStream(fontStream);

        LiveCharts.Configure(settings => settings
            .UseDefaults()
            .HasTextSettings(new TextSettings { DefaultTypeface = typeface }));
    }
}
