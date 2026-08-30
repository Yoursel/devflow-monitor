using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.Model;

public record DashboardLoadResult(
    DashboardSummaryResponse? Summary,
    string? ErrorMessage = null)
{
    public bool IsSuccess => ErrorMessage is null;

    public static DashboardLoadResult Failed(string message) =>
        new(null, message);
}
