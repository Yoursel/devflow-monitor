namespace DevFlowMonitor.Wpf.Dto;

public sealed class GitHubAccountPersistenceDto
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string? ProtectedToken { get; set; }
    public DateTimeOffset? LastSynchronizedAt { get; set; }
}
