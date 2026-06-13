using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.Model;

public record ConnectionCheckResult(
    ConnectionStatus ConnectionStatus,
    string Message,
    ApiHealthStatus? ApiStatus = null,
    string? ApiVersion = null);
