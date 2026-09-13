namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Job
{
    public Guid Id { get; set; }
    public long ExternalId { get; set; }
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;
    public required string Name { get; set; }
    public required string Status { get; set; }
    public string? Conclusion { get; set; }
    public string? RunnerName { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<Step> Steps { get; } = [];
}
