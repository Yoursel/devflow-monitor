namespace DevFlowMonitor.Contracts;

public sealed record GitHubPipelinesRequest(
    string ProfileOrOwner,
    string Token,
    int Page,
    int PageSize);
