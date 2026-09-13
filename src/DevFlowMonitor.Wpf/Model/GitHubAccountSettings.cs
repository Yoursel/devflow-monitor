namespace DevFlowMonitor.Wpf.Model;

public sealed class GitHubAccountSettings
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset? LastSynchronizedAt { get; set; }
}
