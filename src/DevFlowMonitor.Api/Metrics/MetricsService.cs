using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.Data.Entities;
using DevFlowMonitor.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlowMonitor.Api.Metrics;

internal sealed class MetricsService(DevFlowDbContext dbContext) : IMetricsService
{
    public async Task<MetricsSummaryResponse?> GetAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        CancellationToken ct = default)
    {
        if (periodStart >= periodEnd)
            return null;

        var accountExists = await dbContext.GitHubAccounts
            .AnyAsync(account => account.Id == accountId, ct);
        if (!accountExists)
            return null;

        if (repositoryId.HasValue && !await dbContext.Repositories.AnyAsync(
                repository => repository.Id == repositoryId && repository.AccountId == accountId,
                ct))
            return null;

        if (workflowId.HasValue && !await dbContext.Workflows.AnyAsync(
                workflow => workflow.Id == workflowId
                            && workflow.Repository.AccountId == accountId
                            && (!repositoryId.HasValue || workflow.RepositoryId == repositoryId),
                ct))
            return null;

        var duration = periodEnd - periodStart;
        var previousStart = duration > periodStart - DateTimeOffset.MinValue
            ? DateTimeOffset.MinValue
            : periodStart - duration;
        var currentRuns = await CreateRunsQuery(
                accountId,
                periodStart,
                periodEnd,
                repositoryId,
                workflowId)
            .ToArrayAsync(ct);
        var previousRuns = await CreateRunsQuery(
                accountId,
                previousStart,
                periodStart,
                repositoryId,
                workflowId)
            .ToArrayAsync(ct);
        var jobs = await CreateJobsQuery(
                accountId,
                periodStart,
                periodEnd,
                repositoryId,
                workflowId)
            .ToArrayAsync(ct);

        var metrics = MetricsCalculator.Calculate(
            periodStart,
            periodEnd,
            currentRuns,
            previousRuns,
            jobs);

        await StoreSnapshotAsync(accountId, repositoryId, workflowId, metrics, ct);
        return metrics;
    }

    private IQueryable<MetricRunData> CreateRunsQuery(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId)
    {
        var query = dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(run => run.Workflow.Repository.AccountId == accountId
                          && run.StartedAt >= periodStart
                          && run.StartedAt < periodEnd);

        if (repositoryId.HasValue)
            query = query.Where(run => run.Workflow.RepositoryId == repositoryId.Value);
        if (workflowId.HasValue)
            query = query.Where(run => run.WorkflowId == workflowId.Value);

        return query.Select(run => new MetricRunData(
            run.WorkflowId,
            run.Workflow.Repository.FullName + " / " + run.Workflow.Name,
            run.Conclusion,
            run.StartedAt,
            run.CompletedAt,
            run.Attempts.Count));
    }

    private IQueryable<MetricJobData> CreateJobsQuery(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId)
    {
        var query = dbContext.Jobs
            .AsNoTracking()
            .Where(job => job.Attempt.WorkflowRun.Workflow.Repository.AccountId == accountId
                          && job.Attempt.WorkflowRun.StartedAt >= periodStart
                          && job.Attempt.WorkflowRun.StartedAt < periodEnd);

        if (repositoryId.HasValue)
            query = query.Where(job => job.Attempt.WorkflowRun.Workflow.RepositoryId == repositoryId.Value);
        if (workflowId.HasValue)
            query = query.Where(job => job.Attempt.WorkflowRun.WorkflowId == workflowId.Value);

        return query.Select(job => new MetricJobData(
            job.Attempt.WorkflowRun.WorkflowId,
            job.Attempt.WorkflowRun.Workflow.Repository.FullName
            + " / "
            + job.Attempt.WorkflowRun.Workflow.Name,
            job.Name,
            job.Conclusion));
    }

    private async Task StoreSnapshotAsync(
        Guid accountId,
        Guid? repositoryId,
        Guid? workflowId,
        MetricsSummaryResponse summary,
        CancellationToken ct)
    {
        var scopeKey = CreateScopeKey(repositoryId, workflowId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Metrics
            .Where(metric => metric.AccountId == accountId
                             && metric.ScopeKey == scopeKey
                             && metric.PeriodStart == summary.PeriodStart
                             && metric.PeriodEnd == summary.PeriodEnd)
            .ExecuteDeleteAsync(ct);

        var calculatedAt = DateTimeOffset.UtcNow;
        Metric Create(string kind, decimal value, string? dimension = null) => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            RepositoryId = repositoryId,
            WorkflowId = workflowId,
            ScopeKey = scopeKey,
            Kind = kind,
            Dimension = dimension ?? string.Empty,
            Value = value,
            SampleSize = summary.TotalRuns,
            PeriodStart = summary.PeriodStart,
            PeriodEnd = summary.PeriodEnd,
            CalculatedAt = calculatedAt
        };

        Metric[] scalarMetrics =
        [
            Create("total_runs", summary.TotalRuns),
            Create("success_rate", (decimal)summary.SuccessRate),
            Create("failure_rate", (decimal)summary.FailureRate),
            Create("average_duration_seconds", (decimal)summary.AverageDurationSeconds),
            Create("maximum_duration_seconds", (decimal)summary.MaximumDurationSeconds),
            Create("retry_rate", (decimal)summary.RetryRate),
            Create("success_rate_change", (decimal)summary.SuccessRateChange.GetValueOrDefault())
        ];
        var workflowMetrics = summary.WorkflowFailures.Select(item =>
            Create("workflow_failure_rate", (decimal)item.FailureRate, item.Key));
        var jobMetrics = summary.JobFailures.Select(item =>
            Create("job_failure_rate", (decimal)item.FailureRate, item.Key));

        dbContext.Metrics.AddRange(scalarMetrics.Concat(workflowMetrics).Concat(jobMetrics));
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static string CreateScopeKey(Guid? repositoryId, Guid? workflowId) =>
        workflowId.HasValue
            ? $"workflow:{workflowId:N}"
            : repositoryId.HasValue
                ? $"repository:{repositoryId:N}"
                : "account";
}
