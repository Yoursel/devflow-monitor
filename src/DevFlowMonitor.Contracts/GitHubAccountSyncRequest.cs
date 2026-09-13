namespace DevFlowMonitor.Contracts;

public sealed record GitHubAccountSyncRequest(
    string Token,
    bool FullHistory = true);
