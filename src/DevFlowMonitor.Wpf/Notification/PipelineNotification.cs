using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.Notification;

public sealed record PipelineNotification(
    long RunId,
    string PipelineName,
    string Branch,
    PipelineStatus Status);
