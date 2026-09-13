namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Attempt
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public WorkflowRun WorkflowRun { get; set; } = null!;
    public int Number { get; set; }
    public required string Status { get; set; }
    public string? Conclusion { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<Job> Jobs { get; } = [];
}
