namespace DevFlowMonitor.Contracts;

public sealed record FailureFrequencyResponse(
    string Key,
    string Name,
    int TotalExecutions,
    int FailedExecutions,
    double FailureRate);
