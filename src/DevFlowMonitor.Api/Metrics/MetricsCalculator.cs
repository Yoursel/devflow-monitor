using DevFlowMonitor.Contracts;
using DevFlowMonitor.Api.GitHub;

namespace DevFlowMonitor.Api.Metrics;

internal static class MetricsCalculator
{
    public static MetricsSummaryResponse Calculate(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        IReadOnlyList<MetricRunData> runs,
        IReadOnlyList<MetricRunData> previousRuns,
        IReadOnlyList<MetricJobData> jobs,
        bool compareWithPreviousPeriod = true)
    {
        var total = runs.Count;
        var successful = runs.Count(IsSuccessful);
        var failed = runs.Count(IsFailed);
        var retried = runs.Count(run => run.AttemptCount > 1);
        var successRate = Percentage(successful, total);
        var failureRate = Percentage(failed, total);
        var previousSuccessRate = Percentage(previousRuns.Count(IsSuccessful), previousRuns.Count);
        var durations = runs
            .Where(run => run.CompletedAt >= run.StartedAt)
            .Select(run => (run.CompletedAt!.Value - run.StartedAt).TotalSeconds)
            .ToArray();

        return new MetricsSummaryResponse(
            periodStart,
            periodEnd,
            total,
            successful,
            failed,
            successRate,
            failureRate,
            durations.Length == 0 ? 0 : Math.Round(durations.Average(), 2),
            durations.Length == 0 ? 0 : Math.Round(durations.Max(), 2),
            retried,
            Percentage(retried, total),
            compareWithPreviousPeriod
                ? Math.Round(successRate - previousSuccessRate, 2)
                : null,
            CalculateWorkflowFailures(runs),
            CalculateJobFailures(jobs));
    }

    private static IReadOnlyList<FailureFrequencyResponse> CalculateWorkflowFailures(
        IEnumerable<MetricRunData> runs) =>
        runs.GroupBy(run => new { run.WorkflowId, run.WorkflowName })
            .Select(group => CreateFrequency(
                group.Key.WorkflowId.ToString(),
                group.Key.WorkflowName,
                group.Count(),
                group.Count(IsFailed)))
            .OrderByDescending(item => item.FailureRate)
            .ThenByDescending(item => item.FailedExecutions)
            .ThenBy(item => item.Name)
            .ToArray();

    private static IReadOnlyList<FailureFrequencyResponse> CalculateJobFailures(
        IEnumerable<MetricJobData> jobs) =>
        jobs.GroupBy(
                job => new JobGroupKey(job.WorkflowId, job.WorkflowName, job.Name),
                JobGroupKeyComparer.Instance)
            .Select(group => CreateFrequency(
                $"{group.Key.WorkflowId:N}:{group.Key.JobName}",
                $"{group.Key.WorkflowName} / {group.Key.JobName}",
                group.Count(),
                group.Count(job => IsFailed(job.Conclusion))))
            .OrderByDescending(item => item.FailureRate)
            .ThenByDescending(item => item.FailedExecutions)
            .ThenBy(item => item.Name)
            .ToArray();

    private static FailureFrequencyResponse CreateFrequency(
        string key,
        string name,
        int total,
        int failed) =>
        new(key, name, total, failed, Percentage(failed, total));

    private static bool IsSuccessful(MetricRunData run) =>
        GitHubRunOutcome.IsSuccessful(run.Conclusion);

    private static bool IsFailed(MetricRunData run) =>
        GitHubRunOutcome.IsFailed(run.Conclusion);

    private static bool IsFailed(string? conclusion) => GitHubRunOutcome.IsFailed(conclusion);

    private static double Percentage(int value, int total) =>
        total == 0 ? 0 : Math.Round(value * 100d / total, 2);

    private sealed record JobGroupKey(Guid WorkflowId, string WorkflowName, string JobName);

    private sealed class JobGroupKeyComparer : IEqualityComparer<JobGroupKey>
    {
        public static JobGroupKeyComparer Instance { get; } = new();

        public bool Equals(JobGroupKey? x, JobGroupKey? y) =>
            x is not null
            && y is not null
            && x.WorkflowId == y.WorkflowId
            && string.Equals(x.JobName, y.JobName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(JobGroupKey value) => HashCode.Combine(
            value.WorkflowId,
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.JobName));
    }
}
