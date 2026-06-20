namespace DevFlowMonitor.Contracts;

public record PipelineSummaryResponse(
    Guid Id,
    string PipelineName,
    string Branch,
    PipelineStatus Status,
    DateTimeOffset StartedAt,
    int SuccessfulRuns,
    int FailedRuns);