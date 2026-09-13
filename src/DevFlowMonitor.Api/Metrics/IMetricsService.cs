using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.Metrics;

internal interface IMetricsService
{
    Task<MetricsSummaryResponse?> GetAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        CancellationToken ct = default);
}
