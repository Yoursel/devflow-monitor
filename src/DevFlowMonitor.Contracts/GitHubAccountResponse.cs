namespace DevFlowMonitor.Contracts;

public sealed record GitHubAccountResponse(
    Guid Id,
    string Owner,
    int RepositoryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSynchronizedAt);
