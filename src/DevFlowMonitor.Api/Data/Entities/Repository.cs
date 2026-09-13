namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Repository
{
    public Guid Id { get; set; }
    public long ExternalId { get; set; }
    public Guid AccountId { get; set; }
    public GitHubAccount Account { get; set; } = null!;
    public required string Owner { get; set; }
    public required string Name { get; set; }
    public required string FullName { get; set; }
    public required string DefaultBranch { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }
    public ICollection<Workflow> Workflows { get; } = [];
    public ICollection<TriggerEvent> Events { get; } = [];
    public ICollection<GitCommit> Commits { get; } = [];
    public ICollection<Metric> Metrics { get; } = [];
}
