namespace DevFlowMonitor.Wpf.Dto;

public class AppSettingsPersistenceDto
{
    public string ApiUrl { get; set; } = string.Empty;
    public string GitHubProfile { get; set; } = string.Empty;
    public string? ProtectedGitHubToken { get; set; }
    public string? ProtectedPassword { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnSuccess { get; set; }
    public int PollingIntervalSeconds { get; set; } = 60;
}
