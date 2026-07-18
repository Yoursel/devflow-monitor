namespace DevFlowMonitor.Contracts;

public sealed record GitHubConnectionResponse(
    string Owner,
    int RepositoryCount);
