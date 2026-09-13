using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.Data.Entities;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlowMonitor.Api.Pipelines;

internal sealed class StoredPipelineService(DevFlowDbContext dbContext) : IStoredPipelineService
{
    public async Task<PagedResponse<PipelineSummaryResponse>> GetPipelinesAsync(
        Guid accountId,
        int page,
        int pageSize,
        string? search,
        string? branch,
        PipelineStatus? status,
        CancellationToken ct = default)
    {
        var query = dbContext.Workflows
            .AsNoTracking()
            .Where(workflow => workflow.Repository.AccountId == accountId)
            .Where(workflow => workflow.Runs.Any());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(workflow =>
                EF.Functions.ILike(workflow.Name, pattern, "\\")
                || EF.Functions.ILike(workflow.Repository.FullName, pattern, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            var pattern = $"%{EscapeLikePattern(branch.Trim())}%";
            query = query.Where(workflow => EF.Functions.ILike(
                workflow.Runs
                    .OrderByDescending(run => run.StartedAt)
                    .Select(run => run.Branch)
                    .First(),
                pattern,
                "\\"));
        }

        query = ApplyStatusFilter(query, status);

        var totalItems = await query.CountAsync(ct);
        var pageItems = await query
            .Select(workflow => new
            {
                workflow.Id,
                LatestRunStartedAt = workflow.Runs.Max(run => run.StartedAt)
            })
            .OrderByDescending(workflow => workflow.LatestRunStartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);

        var workflowIds = pageItems.Select(item => item.Id).ToArray();
        var workflows = await dbContext.Workflows
            .AsNoTracking()
            .AsSplitQuery()
            .Where(workflow => workflowIds.Contains(workflow.Id))
            .Include(workflow => workflow.Repository)
            .Include(workflow => workflow.Runs)
            .ToDictionaryAsync(workflow => workflow.Id, ct);

        var pipelines = pageItems
            .Select(item => MapWorkflow(workflows[item.Id]))
            .ToArray();

        return new PagedResponse<PipelineSummaryResponse>(
            pipelines,
            page,
            pageSize,
            totalItems);
    }

    public async Task<DashboardSummaryResponse?> GetDashboardAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default)
    {
        if (periodStart >= periodEnd)
            return null;

        var accountExists = await dbContext.GitHubAccounts
            .AnyAsync(account => account.Id == accountId, ct);
        if (!accountExists)
            return null;

        var runs = await dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(run => run.Workflow.Repository.AccountId == accountId
                          && run.StartedAt >= periodStart
                          && run.StartedAt < periodEnd)
            .Include(run => run.Workflow)
                .ThenInclude(workflow => workflow.Repository)
            .OrderByDescending(run => run.StartedAt)
            .ToArrayAsync(ct);
        var recent = runs.Take(4).Select(MapRunAsPipeline).ToArray();

        return new DashboardSummaryResponse(
            runs.Length,
            runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Success),
            runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Failed),
            recent);
    }

    public async Task<RunDetailsResponse?> GetRunDetailsAsync(
        Guid accountId,
        Guid runId,
        CancellationToken ct = default)
    {
        var run = await dbContext.WorkflowRuns
            .AsNoTracking()
            .AsSplitQuery()
            .Where(item => item.Id == runId && item.Workflow.Repository.AccountId == accountId)
            .Include(item => item.Workflow)
                .ThenInclude(workflow => workflow.Repository)
            .Include(item => item.Event)
            .Include(item => item.Commit)
            .Include(item => item.Actor)
            .Include(item => item.Attempts)
                .ThenInclude(attempt => attempt.Jobs)
                .ThenInclude(job => job.Steps)
            .SingleOrDefaultAsync(ct);

        return run is null ? null : MapRunDetails(run);
    }

    private static PipelineSummaryResponse MapWorkflow(Workflow workflow)
    {
        var runs = workflow.Runs.OrderByDescending(run => run.StartedAt).ToArray();
        var latest = runs[0];
        return new PipelineSummaryResponse(
            workflow.Id,
            $"{workflow.Repository.FullName} / {workflow.Name}",
            latest.Branch,
            GitHubRunOutcome.ToPipelineStatus(latest.Status, latest.Conclusion),
            latest.StartedAt,
            runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Success),
            runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Failed),
            runs.Select(MapRun).ToArray());
    }

    private static PipelineSummaryResponse MapRunAsPipeline(WorkflowRun run) =>
        new(
            run.Id,
            $"{run.Workflow.Repository.FullName} / {run.Workflow.Name}",
            run.Branch,
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion),
            run.StartedAt,
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Success ? 1 : 0,
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Failed ? 1 : 0,
            [MapRun(run)]);

    private static PipelineRunResponse MapRun(WorkflowRun run) =>
        new(
            run.ExternalId,
            run.RunNumber,
            run.DisplayTitle,
            run.Branch,
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion),
            run.StartedAt,
            run.Attempts.Count,
            GitHubRunOutcome.ClassifyRerun(run.Attempts.Count, run.Conclusion));

    private static RunDetailsResponse MapRunDetails(WorkflowRun run) =>
        new(
            run.Id,
            run.ExternalId,
            run.RunNumber,
            run.Workflow.Repository.FullName,
            run.Workflow.Name,
            run.DisplayTitle,
            run.Branch,
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion),
            run.Conclusion,
            run.Event?.Name,
            run.Commit?.Sha,
            run.Commit?.Message,
            run.Actor?.Login,
            run.StartedAt,
            run.CompletedAt,
            run.HtmlUrl,
            run.Attempts
                .OrderBy(attempt => attempt.Number)
                .Select(MapAttempt)
                .ToArray(),
            GitHubRunOutcome.ClassifyRerun(run.Attempts.Count, run.Conclusion));

    private static AttemptDetailsResponse MapAttempt(Attempt attempt) =>
        new(
            attempt.Id,
            attempt.Number,
            GitHubRunOutcome.ToPipelineStatus(attempt.Status, attempt.Conclusion),
            attempt.Conclusion,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.Jobs
                .OrderBy(job => job.StartedAt)
                .ThenBy(job => job.Name)
                .Select(MapJob)
                .ToArray());

    private static JobDetailsResponse MapJob(Job job) =>
        new(
            job.Id,
            job.ExternalId,
            job.Name,
            GitHubRunOutcome.ToPipelineStatus(job.Status, job.Conclusion),
            job.Conclusion,
            job.RunnerName,
            job.StartedAt,
            job.CompletedAt,
            job.Steps
                .OrderBy(step => step.Number)
                .Select(MapStep)
                .ToArray());

    private static StepDetailsResponse MapStep(Step step) =>
        new(
            step.Id,
            step.Number,
            step.Name,
            GitHubRunOutcome.ToPipelineStatus(step.Status, step.Conclusion),
            step.Conclusion,
            step.StartedAt,
            step.CompletedAt);

    private static IQueryable<Workflow> ApplyStatusFilter(
        IQueryable<Workflow> query,
        PipelineStatus? status)
    {
        if (!status.HasValue)
            return query;

        return status.Value switch
        {
            PipelineStatus.Running => query.Where(workflow => workflow.Runs
                .OrderByDescending(run => run.StartedAt)
                .Select(run => run.Status)
                .First() != "completed"),
            PipelineStatus.Success => query.Where(workflow => workflow.Runs
                .OrderByDescending(run => run.StartedAt)
                .Select(run => run.Conclusion)
                .First() == "success"),
            PipelineStatus.Cancelled => query.Where(workflow => new[] { "cancelled", "skipped", "neutral" }
                .Contains(workflow.Runs
                    .OrderByDescending(run => run.StartedAt)
                    .Select(run => run.Conclusion)
                    .First())),
            PipelineStatus.Failed => query.Where(workflow => GitHubRunOutcome.FailureConclusions.Contains(
                workflow.Runs
                    .OrderByDescending(run => run.StartedAt)
                    .Select(run => run.Conclusion)
                    .First())),
            _ => query
        };
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
