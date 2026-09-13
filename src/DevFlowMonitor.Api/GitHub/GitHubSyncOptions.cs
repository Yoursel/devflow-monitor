namespace DevFlowMonitor.Api.GitHub;

internal sealed class GitHubSyncOptions
{
    public const string SectionName = "GitHubSync";

    public int MaximumRepositoryPages { get; init; } = 5;
    public int MaximumRunPagesPerRepository { get; init; } = 5;
    public int RunsPerPage { get; init; } = 100;
    public int RunsWithJobDetailsPerRepository { get; init; } = 20;
}
