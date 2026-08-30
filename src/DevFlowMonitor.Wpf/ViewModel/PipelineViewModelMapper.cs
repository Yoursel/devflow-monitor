using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.ViewModel;

internal static class PipelineViewModelMapper
{
    public static PipelineViewModel Map(
        PipelineSummaryResponse pipeline,
        Action<PipelineViewModel>? openHistory = null) =>
        new(
            pipeline.Status,
            pipeline.PipelineName,
            pipeline.Branch,
            FormatTimeAgo(pipeline.StartedAt),
            pipeline.SuccessfulRuns,
            pipeline.FailedRuns,
            pipeline.Runs ?? [],
            openHistory);

    private static string FormatTimeAgo(DateTimeOffset startedAt)
    {
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        if (elapsed.TotalMinutes < 1)
            return "только что";

        return elapsed.TotalHours < 1
            ? $"{(int)elapsed.TotalMinutes} мин. назад"
            : $"{(int)elapsed.TotalHours} ч. назад";
    }
}
