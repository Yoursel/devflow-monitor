namespace DevFlowMonitor.Contracts;

public sealed record CreateGitHubAccountRequest(
    string ProfileOrOwner,
    string Token);
