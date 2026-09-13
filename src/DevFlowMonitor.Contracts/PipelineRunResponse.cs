namespace DevFlowMonitor.Contracts;

public sealed record PipelineRunResponse(
    long Id,
    long RunNumber,
    string Title,
    string Branch,
    PipelineStatus Status,
    DateTimeOffset StartedAt,
    int AttemptCount = 1,
    RerunOutcome RerunOutcome = RerunOutcome.NotRetried);
