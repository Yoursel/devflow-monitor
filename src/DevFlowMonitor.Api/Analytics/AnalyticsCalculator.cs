using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.Analytics;

internal static class AnalyticsCalculator
{
    private const int MaximumTrendPoints = 240;

    public static IReadOnlyList<AnalyticsComparisonResponse> CompareRepositories(
        IEnumerable<AnalyticsRunData> runs) =>
        CreateComparisons(runs, run => (run.RepositoryId, run.RepositoryName));

    public static IReadOnlyList<AnalyticsComparisonResponse> CompareWorkflows(
        IEnumerable<AnalyticsRunData> runs) =>
        CreateComparisons(
            runs,
            run => (run.WorkflowId, $"{run.RepositoryName} / {run.WorkflowName}"));

    public static IReadOnlyList<AnalyticsTrendPointResponse> CreateTrend(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        IEnumerable<AnalyticsRunData> runs)
    {
        var firstDate = DateOnly.FromDateTime(periodStart.UtcDateTime);
        var lastDate = DateOnly.FromDateTime(periodEnd.AddTicks(-1).UtcDateTime);
        var totalDays = lastDate.DayNumber - firstDate.DayNumber + 1;
        var bucketDays = Math.Max(1, (int)Math.Ceiling(totalDays / (double)MaximumTrendPoints));
        var bucketCount = (int)Math.Ceiling(totalDays / (double)bucketDays);
        var groups = runs
            .GroupBy(run =>
                (DateOnly.FromDateTime(run.StartedAt.UtcDateTime).DayNumber - firstDate.DayNumber) / bucketDays)
            .ToDictionary(group => group.Key, group => group.ToArray());
        List<AnalyticsTrendPointResponse> result = [];

        for (var bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            var bucketStart = firstDate.AddDays(bucketIndex * bucketDays);
            var actualBucketDays = Math.Min(bucketDays, lastDate.DayNumber - bucketStart.DayNumber + 1);
            var bucketRuns = groups.GetValueOrDefault(bucketIndex, []);
            var durations = GetDurations(bucketRuns);
            result.Add(new AnalyticsTrendPointResponse(
                bucketStart,
                bucketRuns.Length,
                bucketRuns.Count(run => GitHubRunOutcome.IsSuccessful(run.Conclusion)),
                bucketRuns.Count(run => GitHubRunOutcome.IsFailed(run.Conclusion)),
                durations.Length == 0 ? 0 : Math.Round(durations.Average(), 2),
                actualBucketDays));
        }

        return result;
    }

    private static IReadOnlyList<AnalyticsComparisonResponse> CreateComparisons(
        IEnumerable<AnalyticsRunData> runs,
        Func<AnalyticsRunData, (Guid Id, string Name)> keySelector) =>
        runs.GroupBy(keySelector)
            .Select(group =>
            {
                var items = group.ToArray();
                var durations = GetDurations(items);
                return new AnalyticsComparisonResponse(
                    group.Key.Id,
                    group.Key.Name,
                    items.Length,
                    Percentage(items.Count(run => GitHubRunOutcome.IsSuccessful(run.Conclusion)), items.Length),
                    Percentage(items.Count(run => GitHubRunOutcome.IsFailed(run.Conclusion)), items.Length),
                    durations.Length == 0 ? 0 : Math.Round(durations.Average(), 2),
                    durations.Length == 0 ? 0 : Math.Round(durations.Max(), 2),
                    Percentage(items.Count(run => run.AttemptCount > 1), items.Length));
            })
            .OrderByDescending(item => item.FailureRate)
            .ThenByDescending(item => item.TotalRuns)
            .ThenBy(item => item.Name)
            .ToArray();

    private static double[] GetDurations(IEnumerable<AnalyticsRunData> runs) =>
        runs.Where(run => run.CompletedAt >= run.StartedAt)
            .Select(run => (run.CompletedAt!.Value - run.StartedAt).TotalSeconds)
            .ToArray();

    private static double Percentage(int value, int total) =>
        total == 0 ? 0 : Math.Round(value * 100d / total, 2);
}
