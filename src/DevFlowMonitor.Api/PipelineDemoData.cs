using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api;

internal static class PipelineDemoData
{
    public static IReadOnlyList<PipelineSummaryResponse> Pipelines { get; } =
    [
        new(
            Id: Guid.NewGuid(),
            PipelineName: "backend-ci",
            Branch: "main",
            Status: PipelineStatus.Success,
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            SuccessfulRuns: 18,
            FailedRuns: 1),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "frontend-deploy",
            Branch: "feature/auth",
            Status: PipelineStatus.Failed,
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-12),
            SuccessfulRuns: 7,
            FailedRuns: 3),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "data-pipeline",
            Branch: "develop",
            Status: PipelineStatus.Running,
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-28),
            SuccessfulRuns: 11,
            FailedRuns: 2),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "auth-service",
            Branch: "main",
            Status: PipelineStatus.Success,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            SuccessfulRuns: 16,
            FailedRuns: 0),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "notifications",
            Branch: "main",
            Status: PipelineStatus.Success,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-2),
            SuccessfulRuns: 14,
            FailedRuns: 1),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "billing-api",
            Branch: "release",
            Status: PipelineStatus.Failed,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-3),
            SuccessfulRuns: 9,
            FailedRuns: 4),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "mobile-build",
            Branch: "develop",
            Status: PipelineStatus.Success,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-4),
            SuccessfulRuns: 12,
            FailedRuns: 2),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "docs-site",
            Branch: "main",
            Status: PipelineStatus.Running,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-5),
            SuccessfulRuns: 6,
            FailedRuns: 0),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "worker-cleanup",
            Branch: "main",
            Status: PipelineStatus.Success,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-6),
            SuccessfulRuns: 10,
            FailedRuns: 1),
        new(
            Id: Guid.NewGuid(),
            PipelineName: "integration-tests",
            Branch: "develop",
            Status: PipelineStatus.Failed,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-7),
            SuccessfulRuns: 8,
            FailedRuns: 5)
    ];
}