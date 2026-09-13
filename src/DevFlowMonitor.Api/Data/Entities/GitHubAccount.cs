namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class GitHubAccount
{
    public Guid Id { get; set; }
    public required string Owner { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSynchronizedAt { get; set; }
    public ICollection<Repository> Repositories { get; } = [];
    public ICollection<Metric> Metrics { get; } = [];
}
