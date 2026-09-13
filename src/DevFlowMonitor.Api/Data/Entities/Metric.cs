namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class Metric
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public GitHubAccount Account { get; set; } = null!;
    public Guid? RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public Guid? WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }
    public required string ScopeKey { get; set; }
    public required string Kind { get; set; }
    public required string Dimension { get; set; }
    public decimal Value { get; set; }
    public int SampleSize { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
}
