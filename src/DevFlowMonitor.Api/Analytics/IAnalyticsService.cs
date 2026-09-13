using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.Analytics;

internal interface IAnalyticsService
{
    Task<AnalyticsResponse?> GetAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status,
        bool allTime,
        CancellationToken ct = default);
}
