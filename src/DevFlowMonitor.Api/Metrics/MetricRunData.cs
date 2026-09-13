namespace DevFlowMonitor.Api.Metrics;

internal sealed record MetricRunData(
    Guid WorkflowId,
    string WorkflowName,
    string? Conclusion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int AttemptCount);

internal sealed record MetricJobData(
    Guid WorkflowId,
    string WorkflowName,
    string Name,
    string? Conclusion);
