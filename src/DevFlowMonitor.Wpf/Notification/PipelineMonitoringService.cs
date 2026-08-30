using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Notification;

public sealed class PipelineMonitoringService(
    IDevFlowApiClient apiClient,
    IAppSettingsService settingsService,
    PipelineNotificationDetector detector,
    IDesktopNotificationService notifications,
    ILogger<PipelineMonitoringService> logger) : BackgroundService
{
    private static readonly TimeSpan DisabledPollingInterval = TimeSpan.FromSeconds(15);
    private const int PipelinesPageSize = 50;
    private const int MinimumPollingIntervalSeconds = 30;
    private const int MaximumPollingIntervalSeconds = 3600;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var settings = settingsService.Load();

                if (!settings.NotificationsEnabled)
                {
                    detector.Reset();
                    await Task.Delay(DisabledPollingInterval, stoppingToken);
                    continue;
                }

                await MonitorOnceAsync(settings.NotifyOnSuccess, stoppingToken);

                var interval = TimeSpan.FromSeconds(Math.Clamp(
                    settings.PollingIntervalSeconds,
                    MinimumPollingIntervalSeconds,
                    MaximumPollingIntervalSeconds));
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown of the hosted service.
        }
    }

    private async Task MonitorOnceAsync(bool notifyOnSuccess, CancellationToken ct)
    {
        try
        {
            var result = await apiClient.GetPipelinesAsync(
                page: 1,
                pageSize: PipelinesPageSize,
                ct: ct);

            if (!result.IsSuccess)
            {
                logger.LogWarning(
                    "Background pipeline monitoring failed: {ErrorMessage}",
                    result.ErrorMessage);
                return;
            }

            foreach (var notification in detector.Detect(result.Items))
            {
                if (ShouldShow(notification, notifyOnSuccess))
                    notifications.Show(notification);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during background pipeline monitoring");
        }
    }

    private static bool ShouldShow(PipelineNotification notification, bool notifyOnSuccess) =>
        notification.Status != PipelineStatus.Success || notifyOnSuccess;
}
