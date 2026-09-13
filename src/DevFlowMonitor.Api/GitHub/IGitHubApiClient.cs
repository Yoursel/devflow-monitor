namespace DevFlowMonitor.Api.GitHub;

internal interface IGitHubApiClient
{
    Task<GitHubActionsResult<T>> GetAsync<T>(
        string relativeUrl,
        string token,
        string operationName,
        string failedMessage,
        CancellationToken ct = default);
}
