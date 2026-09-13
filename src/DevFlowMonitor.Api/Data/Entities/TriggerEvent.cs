namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class TriggerEvent
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    public required string Name { get; set; }
    public ICollection<WorkflowRun> Runs { get; } = [];
}
