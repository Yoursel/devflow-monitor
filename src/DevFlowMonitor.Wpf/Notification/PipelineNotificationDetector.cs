using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.Notification;

public sealed class PipelineNotificationDetector
{
    private readonly Dictionary<long, PipelineStatus> _knownRuns = [];
    private bool _initialized;

    public IReadOnlyList<PipelineNotification> Detect(
        IReadOnlyList<PipelineSummaryResponse> pipelines)
    {
        List<PipelineNotification> notifications = [];

        foreach (var pipeline in pipelines)
        {
            foreach (var run in pipeline.Runs ?? [])
            {
                var wasKnown = _knownRuns.TryGetValue(run.Id, out var previousStatus);
                _knownRuns[run.Id] = run.Status;

                if (!_initialized || !IsCompleted(run.Status))
                    continue;

                if (!wasKnown || previousStatus == PipelineStatus.Running)
                {
                    notifications.Add(new PipelineNotification(
                        run.Id,
                        pipeline.PipelineName,
                        run.Branch,
                        run.Status));
                }
            }
        }

        _initialized = true;
        return notifications;
    }

    public void Reset()
    {
        _knownRuns.Clear();
        _initialized = false;
    }

    private static bool IsCompleted(PipelineStatus status) =>
        status is PipelineStatus.Success or PipelineStatus.Failed or PipelineStatus.Cancelled;
}
