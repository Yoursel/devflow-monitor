namespace DevFlowMonitor.Contracts;

public record DashboardSummaryResponse(
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    IReadOnlyList<PipelineSummaryResponse> RecentPipelines);