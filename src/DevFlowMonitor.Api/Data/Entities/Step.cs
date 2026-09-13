namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Step
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public int Number { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public string? Conclusion { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
