namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class WorkflowRun
{
    public Guid Id { get; set; }
    public long ExternalId { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public long RunNumber { get; set; }
    public required string DisplayTitle { get; set; }
    public required string Branch { get; set; }
    public required string Status { get; set; }
    public string? Conclusion { get; set; }
    public Guid? EventId { get; set; }
    public TriggerEvent? Event { get; set; }
    public Guid? CommitId { get; set; }
    public GitCommit? Commit { get; set; }
    public Guid? ActorId { get; set; }
    public GitHubUser? Actor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? HtmlUrl { get; set; }
    public ICollection<Attempt> Attempts { get; } = [];
}
