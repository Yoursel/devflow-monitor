using DevFlowMonitor.Contracts;
using CommunityToolkit.WinUI.Notifications;

namespace DevFlowMonitor.Wpf.Notification;

public sealed class WindowsDesktopNotificationService : IDesktopNotificationService
{
    public void Show(PipelineNotification notification)
    {
        var status = notification.Status switch
        {
            PipelineStatus.Success => "завершён успешно",
            PipelineStatus.Failed => "завершился с ошибкой",
            PipelineStatus.Cancelled => "отменён",
            _ => "изменил состояние"
        };

        new ToastContentBuilder()
            .AddText("DevFlow Monitor")
            .AddText($"{notification.PipelineName} {status}")
            .AddText($"Ветка: {notification.Branch}")
            .Show();
    }
}
