using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.GitHub;

internal interface IGitHubSynchronizationService
{
    Task<GitHubActionsResult<GitHubSyncResponse>> SynchronizeAsync(
        Guid accountId,
        string token,
        bool fullHistory,
        CancellationToken ct = default);
}
