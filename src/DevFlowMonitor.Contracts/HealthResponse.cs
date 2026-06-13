namespace DevFlowMonitor.Contracts;

public record HealthResponse(
    ApiHealthStatus Status,
    string Version,
    DateTimeOffset Timestamp);
