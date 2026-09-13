namespace DevFlowMonitor.Contracts;

public sealed record GitHubSyncResponse(
    Guid AccountId,
    int Repositories,
    int Workflows,
    int Runs,
    int Attempts,
    int Jobs,
    int Steps,
    DateTimeOffset SynchronizedAt,
    IReadOnlyList<string> Warnings);
