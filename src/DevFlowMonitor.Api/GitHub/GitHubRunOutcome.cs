using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.GitHub;

internal static class GitHubRunOutcome
{
    public static readonly string[] FailureConclusions =
    [
        "failure",
        "timed_out",
        "startup_failure",
        "action_required",
        "stale"
    ];

    public static PipelineStatus ToPipelineStatus(string? status, string? conclusion)
    {
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            return PipelineStatus.Running;

        return conclusion?.Trim().ToLowerInvariant() switch
        {
            "success" => PipelineStatus.Success,
            "cancelled" or "skipped" or "neutral" => PipelineStatus.Cancelled,
            _ => PipelineStatus.Failed
        };
    }

    public static bool IsSuccessful(string? conclusion) =>
        string.Equals(conclusion, "success", StringComparison.OrdinalIgnoreCase);

    public static bool IsFailed(string? conclusion) =>
        conclusion is not null
        && FailureConclusions.Contains(conclusion.Trim(), StringComparer.OrdinalIgnoreCase);

    public static RerunOutcome ClassifyRerun(int attemptCount, string? finalConclusion)
    {
        if (attemptCount <= 1)
            return RerunOutcome.NotRetried;

        if (IsSuccessful(finalConclusion))
            return RerunOutcome.SucceededAfterRetry;

        return IsFailed(finalConclusion)
            ? RerunOutcome.StillFailingAfterRetry
            : RerunOutcome.RetriedWithOtherOutcome;
    }
}
