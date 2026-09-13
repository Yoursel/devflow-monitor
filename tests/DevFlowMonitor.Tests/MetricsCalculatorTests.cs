using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Api.Metrics;
using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Tests;

public class MetricsCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsAllRequiredRunAndFailureMetrics()
    {
        var start = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var workflowA = Guid.NewGuid();
        var workflowB = Guid.NewGuid();
        MetricRunData[] runs =
        [
            Run(workflowA, "CI", "success", start, 60, 1),
            Run(workflowA, "CI", "failure", start.AddHours(1), 120, 2),
            Run(workflowB, "Deploy", "success", start.AddHours(2), 300, 1),
            Run(workflowB, "Deploy", "cancelled", start.AddHours(3), 30, 1)
        ];
        MetricRunData[] previousRuns =
        [
            Run(workflowA, "CI", "success", start.AddDays(-2), 60, 1),
            Run(workflowA, "CI", "failure", start.AddDays(-1), 60, 1)
        ];
        MetricJobData[] jobs =
        [
            new(workflowA, "CI", "Build", "success"),
            new(workflowA, "CI", "Build", "failure"),
            new(workflowB, "Deploy", "Test", "success")
        ];

        var result = MetricsCalculator.Calculate(
            start,
            start.AddDays(7),
            runs,
            previousRuns,
            jobs);

        Assert.Equal(4, result.TotalRuns);
        Assert.Equal(2, result.SuccessfulRuns);
        Assert.Equal(1, result.FailedRuns);
        Assert.Equal(50, result.SuccessRate);
        Assert.Equal(25, result.FailureRate);
        Assert.Equal(127.5, result.AverageDurationSeconds);
        Assert.Equal(300, result.MaximumDurationSeconds);
        Assert.Equal(1, result.RetriedRuns);
        Assert.Equal(25, result.RetryRate);
        Assert.Equal(0, result.SuccessRateChange);

        var workflowFailure = Assert.Single(result.WorkflowFailures, item => item.Name == "CI");
        Assert.Equal(2, workflowFailure.TotalExecutions);
        Assert.Equal(1, workflowFailure.FailedExecutions);
        Assert.Equal(50, workflowFailure.FailureRate);

        var jobFailure = Assert.Single(result.JobFailures, item => item.Name == "CI / Build");
        Assert.Equal(2, jobFailure.TotalExecutions);
        Assert.Equal(1, jobFailure.FailedExecutions);
        Assert.Equal(50, jobFailure.FailureRate);
    }

    [Fact]
    public void Calculate_DoesNotMergeSameJobNameAcrossWorkflows()
    {
        var start = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var workflowA = Guid.NewGuid();
        var workflowB = Guid.NewGuid();

        var result = MetricsCalculator.Calculate(
            start,
            start.AddDays(1),
            [],
            [],
            [
                new MetricJobData(workflowA, "CI", "Build", "failure"),
                new MetricJobData(workflowB, "Deploy", "Build", "success")
            ]);

        Assert.Collection(
            result.JobFailures,
            item => Assert.Equal("CI / Build", item.Name),
            item => Assert.Equal("Deploy / Build", item.Name));
    }

    [Fact]
    public void Calculate_WithNoRuns_ReturnsZeroValues()
    {
        var start = DateTimeOffset.Parse("2026-08-01T00:00:00Z");

        var result = MetricsCalculator.Calculate(
            start,
            start.AddDays(1),
            [],
            [],
            []);

        Assert.Equal(0, result.TotalRuns);
        Assert.Equal(0, result.SuccessRate);
        Assert.Equal(0, result.FailureRate);
        Assert.Equal(0, result.AverageDurationSeconds);
        Assert.Equal(0, result.MaximumDurationSeconds);
        Assert.Equal(0, result.RetryRate);
        Assert.Equal(0, result.SuccessRateChange);
        Assert.Empty(result.WorkflowFailures);
        Assert.Empty(result.JobFailures);
    }

    [Fact]
    public void Calculate_CountsAllFailureConclusionsConsistently()
    {
        var start = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var result = MetricsCalculator.Calculate(
            start,
            start.AddDays(1),
            [
                Run(Guid.NewGuid(), "CI", "failure", start, 10, 1),
                Run(Guid.NewGuid(), "CI", "timed_out", start.AddMinutes(1), 10, 1),
                Run(Guid.NewGuid(), "CI", "startup_failure", start.AddMinutes(2), 10, 1),
                Run(Guid.NewGuid(), "CI", "action_required", start.AddMinutes(3), 10, 1),
                Run(Guid.NewGuid(), "CI", "stale", start.AddMinutes(4), 10, 1)
            ],
            [],
            []);

        Assert.Equal(5, result.FailedRuns);
        Assert.Equal(100, result.FailureRate);
    }

    [Fact]
    public void Calculate_ForAllTime_DoesNotCompareWithArtificialPreviousPeriod()
    {
        var start = DateTimeOffset.Parse("2026-08-01T00:00:00Z");

        var result = MetricsCalculator.Calculate(
            start,
            start.AddDays(1),
            [Run(Guid.NewGuid(), "CI", "success", start, 10, 1)],
            [],
            [],
            compareWithPreviousPeriod: false);

        Assert.Null(result.SuccessRateChange);
    }

    [Theory]
    [InlineData(1, "success", RerunOutcome.NotRetried)]
    [InlineData(2, "success", RerunOutcome.SucceededAfterRetry)]
    [InlineData(3, "failure", RerunOutcome.StillFailingAfterRetry)]
    [InlineData(2, "cancelled", RerunOutcome.RetriedWithOtherOutcome)]
    public void ClassifyRerun_ReturnsOutcomeOfFinalAttempt(
        int attemptCount,
        string conclusion,
        RerunOutcome expected)
    {
        Assert.Equal(expected, GitHubRunOutcome.ClassifyRerun(attemptCount, conclusion));
    }

    private static MetricRunData Run(
        Guid workflowId,
        string workflowName,
        string conclusion,
        DateTimeOffset startedAt,
        double durationSeconds,
        int attemptCount) =>
        new(
            workflowId,
            workflowName,
            conclusion,
            startedAt,
            startedAt.AddSeconds(durationSeconds),
            attemptCount);
}
