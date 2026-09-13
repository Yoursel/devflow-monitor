namespace DevFlowMonitor.Contracts;

public sealed record AnalyticsResponse(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    MetricsSummaryResponse Metrics,
    IReadOnlyList<AnalyticsComparisonResponse> RepositoryComparisons,
    IReadOnlyList<AnalyticsComparisonResponse> WorkflowComparisons,
    IReadOnlyList<AnalyticsTrendPointResponse> Trend);

public sealed record AnalyticsComparisonResponse(
    Guid Id,
    string Name,
    int TotalRuns,
    double SuccessRate,
    double FailureRate,
    double AverageDurationSeconds,
    double MaximumDurationSeconds,
    double RetryRate);

public sealed record AnalyticsTrendPointResponse(
    DateOnly Date,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    double AverageDurationSeconds,
    int PeriodDays = 1);
