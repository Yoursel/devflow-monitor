namespace DevFlowMonitor.Contracts;

public sealed record GitHubConnectionRequest(
    string ProfileOrOwner,
    string Token);
