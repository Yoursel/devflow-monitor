using DevFlowMonitor.Api.Analytics;

namespace DevFlowMonitor.Tests;

public sealed class AnalyticsCalculatorTests
{
    [Fact]
    public void CompareRepositories_CalculatesIndependentMetrics()
    {
        var start = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var repositoryA = Guid.NewGuid();
        var repositoryB = Guid.NewGuid();
        var workflowA = Guid.NewGuid();
        var workflowB = Guid.NewGuid();
        AnalyticsRunData[] runs =
        [
            Run(repositoryA, "owner/api", workflowA, "CI", "success", start, 60, 1),
            Run(repositoryA, "owner/api", workflowA, "CI", "failure", start.AddHours(1), 120, 2),
            Run(repositoryB, "owner/ui", workflowB, "Build", "success", start.AddHours(2), 30, 1)
        ];

        var result = AnalyticsCalculator.CompareRepositories(runs);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal(repositoryA, item.Id);
                Assert.Equal(2, item.TotalRuns);
                Assert.Equal(50, item.SuccessRate);
                Assert.Equal(50, item.FailureRate);
                Assert.Equal(90, item.AverageDurationSeconds);
                Assert.Equal(50, item.RetryRate);
            },
            item =>
            {
                Assert.Equal(repositoryB, item.Id);
                Assert.Equal(100, item.SuccessRate);
            });
    }

    [Fact]
    public void CreateTrend_IncludesDaysWithoutRuns()
    {
        var start = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var run = Run(
            Guid.NewGuid(),
            "owner/api",
            Guid.NewGuid(),
            "CI",
            "success",
            start,
            60,
            1);

        var result = AnalyticsCalculator.CreateTrend(start, start.AddDays(3), [run]);

        Assert.Collection(
            result,
            item => Assert.Equal(1, item.TotalRuns),
            item => Assert.Equal(0, item.TotalRuns),
            item => Assert.Equal(0, item.TotalRuns));
    }

    [Fact]
    public void CreateTrend_AggregatesLongPeriodsIntoReadableNumberOfPoints()
    {
        var start = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        AnalyticsRunData[] runs =
        [
            Run(Guid.NewGuid(), "owner/api", Guid.NewGuid(), "CI", "success", start, 60, 1),
            Run(Guid.NewGuid(), "owner/api", Guid.NewGuid(), "CI", "failure", start.AddDays(999), 120, 1)
        ];

        var result = AnalyticsCalculator.CreateTrend(start, start.AddDays(1000), runs);

        Assert.InRange(result.Count, 1, 240);
        Assert.Equal(2, result.Sum(point => point.TotalRuns));
        Assert.All(result, point => Assert.True(point.PeriodDays > 1));
    }

    private static AnalyticsRunData Run(
        Guid repositoryId,
        string repositoryName,
        Guid workflowId,
        string workflowName,
        string conclusion,
        DateTimeOffset startedAt,
        double durationSeconds,
        int attemptCount) =>
        new(
            Guid.NewGuid(),
            repositoryId,
            repositoryName,
            workflowId,
            workflowName,
            conclusion,
            startedAt,
            startedAt.AddSeconds(durationSeconds),
            attemptCount);
}
