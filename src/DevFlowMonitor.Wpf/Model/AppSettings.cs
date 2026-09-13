namespace DevFlowMonitor.Wpf.Model;

public class AppSettings
{
    public string ApiUrl { get; set; } = "http://localhost:5268";
    public string GitHubProfile { get; set; } = string.Empty;
    public string GitHubToken { get; set; } = string.Empty;
    public Guid? ActiveGitHubAccountId { get; set; }
    public List<GitHubAccountSettings> GitHubAccounts { get; set; } = [];
    public bool NotificationsEnabled { get; set; }
    public bool NotifyOnSuccess { get; set; }
    public int PollingIntervalSeconds { get; set; } = 60;
}
