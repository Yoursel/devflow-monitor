namespace DevFlowMonitor.Contracts;

public sealed record RunDetailsResponse(
    Guid Id,
    long ExternalId,
    long RunNumber,
    string RepositoryName,
    string WorkflowName,
    string Title,
    string Branch,
    PipelineStatus Status,
    string? Conclusion,
    string? EventName,
    string? CommitSha,
    string? CommitMessage,
    string? ActorLogin,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? GitHubUrl,
    IReadOnlyList<AttemptDetailsResponse> Attempts,
    RerunOutcome RerunOutcome = RerunOutcome.NotRetried);

public sealed record AttemptDetailsResponse(
    Guid Id,
    int Number,
    PipelineStatus Status,
    string? Conclusion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<JobDetailsResponse> Jobs);

public sealed record JobDetailsResponse(
    Guid Id,
    long ExternalId,
    string Name,
    PipelineStatus Status,
    string? Conclusion,
    string? RunnerName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<StepDetailsResponse> Steps);

public sealed record StepDetailsResponse(
    Guid Id,
    int Number,
    string Name,
    PipelineStatus Status,
    string? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
