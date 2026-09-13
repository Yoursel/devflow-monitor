namespace DevFlowMonitor.Contracts;

public sealed record MetricsSummaryResponse(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    double SuccessRate,
    double FailureRate,
    double AverageDurationSeconds,
    double MaximumDurationSeconds,
    int RetriedRuns,
    double RetryRate,
    double? SuccessRateChange,
    IReadOnlyList<FailureFrequencyResponse> WorkflowFailures,
    IReadOnlyList<FailureFrequencyResponse> JobFailures);
