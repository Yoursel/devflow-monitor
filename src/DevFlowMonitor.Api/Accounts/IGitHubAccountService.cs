using DevFlowMonitor.Contracts;
using DevFlowMonitor.Api.GitHub;

namespace DevFlowMonitor.Api.Accounts;

internal interface IGitHubAccountService
{
    Task<IReadOnlyList<GitHubAccountResponse>> GetAllAsync(CancellationToken ct = default);
    Task<GitHubActionsResult<GitHubAccountResponse>> AddAsync(
        CreateGitHubAccountRequest request,
        CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<RepositoryResponse>> GetRepositoriesAsync(
        Guid accountId,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(
        Guid repositoryId,
        CancellationToken ct = default);
}
