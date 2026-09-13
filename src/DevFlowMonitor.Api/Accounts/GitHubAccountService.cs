using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.Data.Entities;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlowMonitor.Api.Accounts;

internal sealed class GitHubAccountService(
    DevFlowDbContext dbContext,
    IGitHubActionsClient gitHub) : IGitHubAccountService
{
    public async Task<IReadOnlyList<GitHubAccountResponse>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.GitHubAccounts
            .AsNoTracking()
            .OrderBy(account => account.Owner)
            .Select(account => new GitHubAccountResponse(
                account.Id,
                account.Owner,
                account.Repositories.Count,
                account.CreatedAt,
                account.LastSynchronizedAt))
            .ToArrayAsync(ct);

    public async Task<GitHubActionsResult<GitHubAccountResponse>> AddAsync(
        CreateGitHubAccountRequest request,
        CancellationToken ct = default)
    {
        var connection = await gitHub.CheckConnectionAsync(
            new GitHubConnectionRequest(request.ProfileOrOwner, request.Token),
            ct);

        if (!connection.IsSuccess)
            return GitHubActionsResult<GitHubAccountResponse>.Failed(connection.ErrorMessage!);

        var owner = connection.Value!.Owner.Trim().ToLowerInvariant();
        var account = await dbContext.GitHubAccounts
            .Include(item => item.Repositories)
            .SingleOrDefaultAsync(item => item.Owner == owner, ct);

        if (account is null)
        {
            account = new GitHubAccount
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.GitHubAccounts.Add(account);
            await dbContext.SaveChangesAsync(ct);
        }

        return GitHubActionsResult<GitHubAccountResponse>.Success(new GitHubAccountResponse(
            account.Id,
            account.Owner,
            account.Repositories.Count,
            account.CreatedAt,
            account.LastSynchronizedAt));
    }

    public async Task<bool> DeleteAsync(Guid accountId, CancellationToken ct = default)
    {
        var deleted = await dbContext.GitHubAccounts
            .Where(account => account.Id == accountId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<RepositoryResponse>> GetRepositoriesAsync(
        Guid accountId,
        CancellationToken ct = default) =>
        await dbContext.Repositories
            .AsNoTracking()
            .Where(repository => repository.AccountId == accountId)
            .OrderBy(repository => repository.FullName)
            .Select(repository => new RepositoryResponse(
                repository.Id,
                repository.Owner,
                repository.Name,
                repository.FullName,
                repository.DefaultBranch,
                repository.IsPrivate))
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(
        Guid repositoryId,
        CancellationToken ct = default) =>
        await dbContext.Workflows
            .AsNoTracking()
            .Where(workflow => workflow.RepositoryId == repositoryId)
            .OrderBy(workflow => workflow.Name)
            .Select(workflow => new WorkflowResponse(
                workflow.Id,
                workflow.RepositoryId,
                workflow.Name,
                workflow.Path,
                workflow.State))
            .ToArrayAsync(ct);
}
