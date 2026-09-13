namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class GitCommit
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    public required string Sha { get; set; }
    public string? Message { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public DateTimeOffset? AuthoredAt { get; set; }
    public ICollection<WorkflowRun> Runs { get; } = [];
}
