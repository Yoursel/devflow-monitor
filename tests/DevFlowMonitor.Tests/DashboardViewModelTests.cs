using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Tests;

public class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_MapsSummaryAndRecentPipelines()
    {
        var apiClient = new StubApiClient
        {
            DashboardResult = new DashboardLoadResult(
                new DashboardSummaryResponse(
                    TotalRuns: 30,
                    SuccessfulRuns: 24,
                    FailedRuns: 6,
                    RecentPipelines:
                    [
                        new PipelineSummaryResponse(
                            Guid.NewGuid(),
                            "backend-ci",
                            "main",
                            PipelineStatus.Success,
                            DateTimeOffset.UtcNow.AddMinutes(-5),
                            12,
                            1)
                    ])),
            MetricsResult = ApiOperationResult<MetricsSummaryResponse>.Success(new MetricsSummaryResponse(
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow,
                30,
                24,
                6,
                80,
                20,
                60,
                120,
                3,
                10,
                5,
                [],
                []))
        };
        var viewModel = new DashboardViewModel(apiClient);

        await viewModel.LoadAsync();

        Assert.Collection(
            viewModel.StatusCards,
            card => Assert.Equal(30, card.Value),
            card => Assert.Equal(24, card.Value),
            card => Assert.Equal(6, card.Value));

        var pipeline = Assert.Single(viewModel.PipelineRuns);
        Assert.Equal("backend-ci", pipeline.PipelineName);
        Assert.Equal(PipelineStatus.Success, pipeline.Status);
        Assert.Equal("Данные обновлены", viewModel.StatusMessage);
        Assert.Empty(viewModel.MetricsStatusMessage);
        Assert.Equal(80, viewModel.Metrics!.SuccessRate);
    }

    [Fact]
    public async Task LoadAsync_ShowsClientError()
    {
        var viewModel = new DashboardViewModel(new StubApiClient
        {
            DashboardResult = DashboardLoadResult.Failed("Сначала укажите URL API в настройках")
        });

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.PipelineRuns);
        Assert.Equal("Сначала укажите URL API в настройках", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_ShowsMetricsError_WhenDashboardIsAvailable()
    {
        var viewModel = new DashboardViewModel(new StubApiClient
        {
            DashboardResult = new DashboardLoadResult(new DashboardSummaryResponse(4, 1, 3, [])),
            MetricsResult = ApiOperationResult<MetricsSummaryResponse>.Failed(
                "Добавьте GitHub-аккаунт в настройках и выполните синхронизацию")
        });

        await viewModel.LoadAsync();

        Assert.Null(viewModel.Metrics);
        Assert.Equal(
            "Добавьте GitHub-аккаунт в настройках и выполните синхронизацию",
            viewModel.MetricsStatusMessage);
        Assert.Equal("Данные обновлены", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleAllTime_RequestsCompleteDashboardHistory()
    {
        var apiClient = new StubApiClient
        {
            DashboardResult = new DashboardLoadResult(new DashboardSummaryResponse(0, 0, 0, [])),
            MetricsResult = ApiOperationResult<MetricsSummaryResponse>.Success(new MetricsSummaryResponse(
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UtcNow,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                [],
                []))
        };
        var viewModel = new DashboardViewModel(apiClient);

        await ((AsyncRelayCommand)viewModel.ToggleAllTimeCommand).ExecuteAsync();

        Assert.True(viewModel.IsAllTime);
        Assert.False(viewModel.ArePeriodDatesEnabled);
        Assert.Equal(DateTimeOffset.UnixEpoch, apiClient.LastDashboardPeriodStart);
        Assert.Equal(DateTimeOffset.UnixEpoch, apiClient.LastMetricsPeriodStart);
        Assert.Equal("Статистика за всё время", viewModel.StatisticsTitle);
    }

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public DashboardLoadResult DashboardResult { get; init; } =
            DashboardLoadResult.Failed("Not configured");
        public ApiOperationResult<MetricsSummaryResponse> MetricsResult { get; init; } =
            ApiOperationResult<MetricsSummaryResponse>.Failed("Not configured");
        public DateTimeOffset? LastDashboardPeriodStart { get; private set; }
        public DateTimeOffset? LastMetricsPeriodStart { get; private set; }

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl,
            string gitHubProfile,
            string gitHubToken,
            CancellationToken ct = default) =>
            Task.FromResult(new ConnectionCheckResult(ConnectionStatus.Connected, "Connected"));

        public Task<ApiOperationResult<GitHubAccountResponse>> AddGitHubAccountAsync(
            string apiUrl, string gitHubProfile, string gitHubToken, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<GitHubAccountResponse>.Failed("Not configured"));

        public Task<ApiOperationResult<GitHubSyncResponse>> SynchronizeGitHubAccountAsync(
            string apiUrl, Guid accountId, string gitHubToken, bool fullHistory = true, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<GitHubSyncResponse>.Failed("Not configured"));

        public Task<ApiOperationResult<bool>> DeleteGitHubAccountAsync(
            string apiUrl, Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<bool>.Failed("Not configured"));

        public Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(CancellationToken ct = default) =>
            Task.FromResult(MetricsResult);

        public Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken ct = default)
        {
            LastMetricsPeriodStart = periodStart;
            return Task.FromResult(MetricsResult);
        }

        public Task<ApiOperationResult<IReadOnlyList<RepositoryResponse>>> GetRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Failed("Not configured"));

        public Task<ApiOperationResult<IReadOnlyList<WorkflowResponse>>> GetWorkflowsAsync(
            Guid repositoryId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<IReadOnlyList<WorkflowResponse>>.Failed("Not configured"));

        public Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
            DateTimeOffset periodStart, DateTimeOffset periodEnd, Guid? repositoryId = null,
            Guid? workflowId = null, PipelineStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<AnalyticsResponse>.Failed("Not configured"));

        public Task<ApiOperationResult<RunDetailsResponse>> GetRunDetailsAsync(
            Guid runId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<RunDetailsResponse>.Failed("Not configured"));

        public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(DashboardResult);

        public Task<DashboardLoadResult> GetDashboardAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken ct = default)
        {
            LastDashboardPeriodStart = periodStart;
            return Task.FromResult(DashboardResult);
        }

        public Task<PipelinesLoadResult> GetPipelinesAsync(
            int page,
            int pageSize,
            string? search = null,
            string? branch = null,
            PipelineStatus? status = null,
            CancellationToken ct = default) =>
            Task.FromResult(PipelinesLoadResult.Failed("Not configured"));
    }
}
