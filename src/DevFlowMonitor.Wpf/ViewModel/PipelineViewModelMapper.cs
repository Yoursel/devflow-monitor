using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.ViewModel;

internal static class PipelineViewModelMapper
{
    public static PipelineViewModel Map(PipelineSummaryResponse pipeline) =>
        new()
        {
            Status = pipeline.Status,
            PipelineName = pipeline.PipelineName,
            Branch = pipeline.Branch,
            TimeAgo = FormatTimeAgo(pipeline.StartedAt),
            SuccessfulRuns = pipeline.SuccessfulRuns,
            FailedRuns = pipeline.FailedRuns
        };

    private static string FormatTimeAgo(DateTimeOffset startedAt)
    {
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        if (elapsed.TotalMinutes < 1)
            return "только что";

        return elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes} мин. назад" : $"{(int)elapsed.TotalHours} ч. назад";
    }
}