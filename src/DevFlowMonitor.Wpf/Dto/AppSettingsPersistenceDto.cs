namespace DevFlowMonitor.Wpf.Dto;

public class AppSettingsPersistenceDto
{
    public string ApiUrl { get; set; } = string.Empty;
    public string GitHubProfile { get; set; } = string.Empty;
    public string? ProtectedGitHubToken { get; set; }
    public string? ProtectedPassword { get; set; }
}
