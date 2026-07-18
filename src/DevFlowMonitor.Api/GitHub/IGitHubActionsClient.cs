using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.GitHub;

internal interface IGitHubActionsClient
{
    Task<GitHubActionsResult<GitHubConnectionResponse>> CheckConnectionAsync(
        GitHubConnectionRequest request,
        CancellationToken ct = default);

    Task<GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>> GetPipelinesAsync(
        GitHubPipelinesRequest request,
        CancellationToken ct = default);

    Task<GitHubActionsResult<DashboardSummaryResponse>> GetDashboardAsync(
        GitHubConnectionRequest request,
        CancellationToken ct = default);
}
