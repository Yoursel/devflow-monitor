namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Workflow
{
    public Guid Id { get; set; }
    public long ExternalId { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    public required string Name { get; set; }
    public string? Path { get; set; }
    public required string State { get; set; }
    public ICollection<WorkflowRun> Runs { get; } = [];
    public ICollection<Metric> Metrics { get; } = [];
}
