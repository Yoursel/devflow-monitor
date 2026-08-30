using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Notification;

namespace DevFlowMonitor.Tests;

public class PipelineNotificationDetectorTests
{
    private readonly PipelineNotificationDetector _detector = new();

    [Fact]
    public void FirstSnapshot_SeedsStateWithoutNotifications()
    {
        var notifications = _detector.Detect([Pipeline(Run(1, PipelineStatus.Failed))]);

        Assert.Empty(notifications);
    }

    [Fact]
    public void NewCompletedRun_AfterInitialSnapshot_CreatesNotificationOnce()
    {
        _detector.Detect([Pipeline(Run(1, PipelineStatus.Success))]);

        var first = _detector.Detect([Pipeline(
            Run(2, PipelineStatus.Failed),
            Run(1, PipelineStatus.Success))]);
        var duplicate = _detector.Detect([Pipeline(
            Run(2, PipelineStatus.Failed),
            Run(1, PipelineStatus.Success))]);

        Assert.Equal(2, Assert.Single(first).RunId);
        Assert.Empty(duplicate);
    }

    [Fact]
    public void RunningToFailedTransition_CreatesNotification()
    {
        _detector.Detect([Pipeline(Run(7, PipelineStatus.Running))]);

        var notifications = _detector.Detect([Pipeline(Run(7, PipelineStatus.Failed))]);

        var notification = Assert.Single(notifications);
        Assert.Equal(PipelineStatus.Failed, notification.Status);
        Assert.Equal("main", notification.Branch);
    }

    [Fact]
    public void Reset_MakesNextSnapshotASeedAgain()
    {
        _detector.Detect([Pipeline(Run(1, PipelineStatus.Running))]);
        _detector.Reset();

        var notifications = _detector.Detect([Pipeline(Run(1, PipelineStatus.Failed))]);

        Assert.Empty(notifications);
    }

    private static PipelineSummaryResponse Pipeline(params PipelineRunResponse[] runs) =>
        new(
            Guid.NewGuid(),
            "team/backend / CI",
            "main",
            runs[0].Status,
            runs[0].StartedAt,
            runs.Count(run => run.Status == PipelineStatus.Success),
            runs.Count(run => run.Status == PipelineStatus.Failed),
            Runs: runs);

    private static PipelineRunResponse Run(long id, PipelineStatus status) =>
        new(id, id, $"Run {id}", "main", status, DateTimeOffset.UtcNow);
}
