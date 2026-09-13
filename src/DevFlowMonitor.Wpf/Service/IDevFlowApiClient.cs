using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.Service;

public interface IDevFlowApiClient
{
    Task<ConnectionCheckResult> CheckConnectionAsync(
        string apiUrl,
        string gitHubProfile,
        string gitHubToken,
        CancellationToken ct = default);

    Task<ApiOperationResult<GitHubAccountResponse>> AddGitHubAccountAsync(
        string apiUrl,
        string gitHubProfile,
        string gitHubToken,
        CancellationToken ct = default);

    Task<ApiOperationResult<GitHubSyncResponse>> SynchronizeGitHubAccountAsync(
        string apiUrl,
        Guid accountId,
        string gitHubToken,
        bool fullHistory = true,
        CancellationToken ct = default);

    Task<ApiOperationResult<bool>> DeleteGitHubAccountAsync(
        string apiUrl,
        Guid accountId,
        CancellationToken ct = default);

    Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(
        CancellationToken ct = default);

    Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default) => GetMetricsAsync(ct);

    Task<ApiOperationResult<IReadOnlyList<RepositoryResponse>>> GetRepositoriesAsync(
        CancellationToken ct = default);

    Task<ApiOperationResult<IReadOnlyList<WorkflowResponse>>> GetWorkflowsAsync(
        Guid repositoryId,
        CancellationToken ct = default);

    Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId = null,
        Guid? workflowId = null,
        PipelineStatus? status = null,
        CancellationToken ct = default);

    Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status,
        bool allTime,
        CancellationToken ct = default) => GetAnalyticsAsync(
            periodStart,
            periodEnd,
            repositoryId,
            workflowId,
            status,
            ct);

    Task<ApiOperationResult<RunDetailsResponse>> GetRunDetailsAsync(
        Guid runId,
        CancellationToken ct = default);

    Task<DashboardLoadResult> GetDashboardAsync(
        CancellationToken ct = default);

    Task<DashboardLoadResult> GetDashboardAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default) => GetDashboardAsync(ct);

    Task<PipelinesLoadResult> GetPipelinesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? branch = null,
        PipelineStatus? status = null,
        CancellationToken ct = default);

    Task<PipelinesLoadResult> RefreshPipelinesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? branch = null,
        PipelineStatus? status = null,
        CancellationToken ct = default) => GetPipelinesAsync(
            page,
            pageSize,
            search,
            branch,
            status,
            ct);
}
