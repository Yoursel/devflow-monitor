using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.Service;

public interface IDevFlowApiClient
{
    Task<ConnectionCheckResult> CheckConnectionAsync(
        string apiUrl,
        CancellationToken ct = default);

    Task<DashboardLoadResult> GetDashboardAsync(
        CancellationToken ct = default);

    Task<PipelinesLoadResult> GetPipelinesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);
}