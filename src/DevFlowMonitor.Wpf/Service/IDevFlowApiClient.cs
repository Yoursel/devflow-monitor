using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.Service;

public interface IDevFlowApiClient
{
    Task<ConnectionCheckResult> CheckConnectionAsync(
        string apiUrl,
        CancellationToken ct = default);
}
