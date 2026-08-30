namespace DevFlowMonitor.Contracts;

public sealed record PipelineRunResponse(
    long Id,
    long RunNumber,
    string Title,
    string Branch,
    PipelineStatus Status,
    DateTimeOffset StartedAt);
