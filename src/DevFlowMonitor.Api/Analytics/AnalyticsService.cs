using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.Data.Entities;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Api.Metrics;
using DevFlowMonitor.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlowMonitor.Api.Analytics;

internal sealed class AnalyticsService(DevFlowDbContext dbContext) : IAnalyticsService
{
    public async Task<AnalyticsResponse?> GetAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status,
        bool allTime,
        CancellationToken ct = default)
    {
        if (!allTime && periodStart >= periodEnd)
            return null;

        if (!await dbContext.GitHubAccounts.AnyAsync(account => account.Id == accountId, ct))
            return null;

        if (repositoryId.HasValue && !await dbContext.Repositories.AnyAsync(
                repository => repository.Id == repositoryId.Value && repository.AccountId == accountId,
                ct))
            return null;

        if (workflowId.HasValue && !await dbContext.Workflows.AnyAsync(
                workflow => workflow.Id == workflowId.Value
                            && workflow.Repository.AccountId == accountId
                            && (!repositoryId.HasValue || workflow.RepositoryId == repositoryId.Value),
                ct))
            return null;

        if (allTime)
        {
            var earliestRun = await CreateScopedRunsQuery(accountId, repositoryId, workflowId, status)
                .MinAsync(run => (DateTimeOffset?)run.StartedAt, ct);
            periodStart = earliestRun ?? periodEnd.AddDays(-30);
        }

        if (periodStart >= periodEnd)
            return null;

        var currentQuery = CreateRunsQuery(
            accountId,
            periodStart,
            periodEnd,
            repositoryId,
            workflowId,
            status);
        var duration = periodEnd - periodStart;
        var previousPeriodStart = duration > periodStart - DateTimeOffset.MinValue
            ? DateTimeOffset.MinValue
            : periodStart - duration;
        var previousQuery = CreateRunsQuery(
            accountId,
            previousPeriodStart,
            periodStart,
            repositoryId,
            workflowId,
            status);

        var currentRuns = await ProjectRuns(currentQuery).ToArrayAsync(ct);
        var previousRuns = await ProjectMetricRuns(previousQuery).ToArrayAsync(ct);
        var currentRunIds = currentRuns.Select(run => run.Id).ToArray();
        var jobs = await dbContext.Jobs
            .AsNoTracking()
            .Where(job => currentRunIds.Contains(job.Attempt.WorkflowRunId))
            .Select(job => new MetricJobData(
                job.Attempt.WorkflowRun.WorkflowId,
                job.Attempt.WorkflowRun.Workflow.Repository.FullName
                + " / "
                + job.Attempt.WorkflowRun.Workflow.Name,
                job.Name,
                job.Conclusion))
            .ToArrayAsync(ct);

        var metricRuns = currentRuns.Select(ToMetricRun).ToArray();
        var metrics = MetricsCalculator.Calculate(
            periodStart,
            periodEnd,
            metricRuns,
            previousRuns,
            jobs,
            compareWithPreviousPeriod: !allTime);
        return new AnalyticsResponse(
            periodStart,
            periodEnd,
            metrics,
            AnalyticsCalculator.CompareRepositories(currentRuns),
            AnalyticsCalculator.CompareWorkflows(currentRuns),
            AnalyticsCalculator.CreateTrend(periodStart, periodEnd, currentRuns));
    }

    private IQueryable<WorkflowRun> CreateRunsQuery(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status)
    {
        return CreateScopedRunsQuery(accountId, repositoryId, workflowId, status)
            .Where(run => run.StartedAt >= periodStart && run.StartedAt < periodEnd);
    }

    private IQueryable<WorkflowRun> CreateScopedRunsQuery(
        Guid accountId,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status)
    {
        var query = dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(run => run.Workflow.Repository.AccountId == accountId);

        if (repositoryId.HasValue)
            query = query.Where(run => run.Workflow.RepositoryId == repositoryId.Value);
        if (workflowId.HasValue)
            query = query.Where(run => run.WorkflowId == workflowId.Value);

        return ApplyStatusFilter(query, status);
    }

    private static IQueryable<WorkflowRun> ApplyStatusFilter(
        IQueryable<WorkflowRun> query,
        PipelineStatus? status) => status switch
        {
            PipelineStatus.Running => query.Where(run => run.Status != "completed"),
            PipelineStatus.Success => query.Where(run => run.Status == "completed" && run.Conclusion == "success"),
            PipelineStatus.Cancelled => query.Where(run => run.Status == "completed"
                && new[] { "cancelled", "skipped", "neutral" }.Contains(run.Conclusion)),
            PipelineStatus.Failed => query.Where(run => run.Status == "completed"
                && GitHubRunOutcome.FailureConclusions.Contains(run.Conclusion)),
            _ => query
        };

    private static IQueryable<AnalyticsRunData> ProjectRuns(IQueryable<WorkflowRun> query) =>
        query.Select(run => new AnalyticsRunData(
            run.Id,
            run.Workflow.RepositoryId,
            run.Workflow.Repository.FullName,
            run.WorkflowId,
            run.Workflow.Name,
            run.Conclusion,
            run.StartedAt,
            run.CompletedAt,
            run.Attempts.Count));

    private static IQueryable<MetricRunData> ProjectMetricRuns(IQueryable<WorkflowRun> query) =>
        query.Select(run => new MetricRunData(
            run.WorkflowId,
            run.Workflow.Repository.FullName + " / " + run.Workflow.Name,
            run.Conclusion,
            run.StartedAt,
            run.CompletedAt,
            run.Attempts.Count));

    private static MetricRunData ToMetricRun(AnalyticsRunData run) =>
        new(
            run.WorkflowId,
            $"{run.RepositoryName} / {run.WorkflowName}",
            run.Conclusion,
            run.StartedAt,
            run.CompletedAt,
            run.AttemptCount);
}
