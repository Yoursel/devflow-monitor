namespace DevFlowMonitor.Api.Analytics;

internal sealed record AnalyticsRunData(
    Guid Id,
    Guid RepositoryId,
    string RepositoryName,
    Guid WorkflowId,
    string WorkflowName,
    string? Conclusion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int AttemptCount);
