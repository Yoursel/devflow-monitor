namespace DevFlowMonitor.Api.Data.Entities;

internal sealed class GitHubUser
{
    public Guid Id { get; set; }
    public long ExternalId { get; set; }
    public required string Login { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? HtmlUrl { get; set; }
    public ICollection<WorkflowRun> Runs { get; } = [];
}
