using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.Pipelines;

internal interface IStoredPipelineService
{
    Task<PagedResponse<PipelineSummaryResponse>> GetPipelinesAsync(
        Guid accountId,
        int page,
        int pageSize,
        string? search,
        string? branch,
        PipelineStatus? status,
        CancellationToken ct = default);

    Task<DashboardSummaryResponse?> GetDashboardAsync(
        Guid accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default);

    Task<RunDetailsResponse?> GetRunDetailsAsync(
        Guid accountId,
        Guid runId,
        CancellationToken ct = default);
}
