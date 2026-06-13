namespace DevFlowMonitor.Wpf.Dto;

public class AppSettingsPersistenceDto
{
    public string ApiUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? ProtectedPassword { get; set; }
}
