using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Tests;

public sealed class AnalyticsViewModelTests
{
    [Fact]
    public async Task ApplyFilters_LoadsComparisonsAndTrend()
    {
        var repositoryId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var metrics = new MetricsSummaryResponse(
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            2,
            1,
            1,
            50,
            50,
            90,
            120,
            1,
            50,
            10,
            [],
            [new FailureFrequencyResponse("job", "CI / Build", 2, 1, 50)]);
        var client = new StubApiClient
        {
            RepositoriesResult = ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Success(
                [new RepositoryResponse(repositoryId, "owner", "api", "owner/api", "main", false)]),
            WorkflowsResult = ApiOperationResult<IReadOnlyList<WorkflowResponse>>.Success(
                [new WorkflowResponse(workflowId, repositoryId, "CI", null, "active")]),
            AnalyticsResult = ApiOperationResult<AnalyticsResponse>.Success(new AnalyticsResponse(
                metrics.PeriodStart,
                metrics.PeriodEnd,
                metrics,
                [new AnalyticsComparisonResponse(repositoryId, "owner/api", 2, 50, 50, 90, 120, 50)],
                [new AnalyticsComparisonResponse(workflowId, "CI", 2, 50, 50, 90, 120, 50)],
                [new AnalyticsTrendPointResponse(new DateOnly(2026, 9, 1), 2, 1, 1, 90)])),
        };
        var viewModel = new AnalyticsViewModel(client);

        await viewModel.ActivateAsync();
        viewModel.PeriodStartDate = new DateTime(2024, 9, 1);
        viewModel.PeriodEndDate = new DateTime(2026, 9, 1);
        viewModel.SelectedRepository = viewModel.Repositories.Single(item => item.Id == repositoryId);
        await WaitUntil(() => viewModel.Workflows.Any(item => item.Id == workflowId));
        viewModel.SelectedWorkflow = viewModel.Workflows.Single(item => item.Id == workflowId);
        viewModel.SelectedStatus = PipelineStatus.Failed;
        await ((AsyncRelayCommand)viewModel.ApplyFiltersCommand).ExecuteAsync();

        Assert.Equal(repositoryId, client.LastRepositoryId);
        Assert.Equal(workflowId, client.LastWorkflowId);
        Assert.Equal(PipelineStatus.Failed, client.LastStatus);
        Assert.True(client.LastPeriodEnd - client.LastPeriodStart > TimeSpan.FromDays(366));
        Assert.Single(viewModel.RepositoryComparisons);
        Assert.Single(viewModel.WorkflowComparisons);
        Assert.Single(viewModel.Trend);
        Assert.Equal("CI / Build", Assert.Single(viewModel.JobFailures).Name);
    }

    [Fact]
    public async Task ToggleAllTime_LoadsEntireStoredHistoryAndUnlocksDatesWhenDisabled()
    {
        var from = DateTimeOffset.Parse("2020-01-10T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-09-10T00:00:00Z");
        var metrics = new MetricsSummaryResponse(from, to, 12, 9, 3, 75, 25, 60, 120, 0, 0, null, [], []);
        var client = new StubApiClient
        {
            RepositoriesResult = ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Success([]),
            AnalyticsResult = ApiOperationResult<AnalyticsResponse>.Success(
                new AnalyticsResponse(from, to, metrics, [], [], []))
        };
        var viewModel = new AnalyticsViewModel(client);

        await viewModel.ActivateAsync();
        await ((AsyncRelayCommand)viewModel.ToggleAllTimeCommand).ExecuteAsync();

        Assert.True(client.LastAllTime);
        Assert.True(viewModel.IsAllTime);
        Assert.False(viewModel.ArePeriodDatesEnabled);
        Assert.Equal(from.LocalDateTime.Date, viewModel.PeriodStartDate);
        Assert.Equal(to.LocalDateTime.Date.AddDays(-1), viewModel.PeriodEndDate);
        Assert.Contains("за всё время", viewModel.StatusMessage);

        await ((AsyncRelayCommand)viewModel.ToggleAllTimeCommand).ExecuteAsync();

        Assert.False(viewModel.IsAllTime);
        Assert.True(viewModel.ArePeriodDatesEnabled);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public ApiOperationResult<IReadOnlyList<RepositoryResponse>> RepositoriesResult { get; init; } =
            ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Failed("Not configured");
        public ApiOperationResult<IReadOnlyList<WorkflowResponse>> WorkflowsResult { get; init; } =
            ApiOperationResult<IReadOnlyList<WorkflowResponse>>.Failed("Not configured");
        public ApiOperationResult<AnalyticsResponse> AnalyticsResult { get; init; } =
            ApiOperationResult<AnalyticsResponse>.Failed("Not configured");
        public Guid? LastRepositoryId { get; private set; }
        public Guid? LastWorkflowId { get; private set; }
        public PipelineStatus? LastStatus { get; private set; }
        public DateTimeOffset LastPeriodStart { get; private set; }
        public DateTimeOffset LastPeriodEnd { get; private set; }
        public bool LastAllTime { get; private set; }

        public Task<ApiOperationResult<IReadOnlyList<RepositoryResponse>>> GetRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(RepositoriesResult);

        public Task<ApiOperationResult<IReadOnlyList<WorkflowResponse>>> GetWorkflowsAsync(
            Guid repositoryId, CancellationToken ct = default) => Task.FromResult(WorkflowsResult);

        public Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            Guid? repositoryId = null,
            Guid? workflowId = null,
            PipelineStatus? status = null,
            CancellationToken ct = default) => GetAnalyticsAsync(
                periodStart,
                periodEnd,
                repositoryId,
                workflowId,
                status,
                false,
                ct);

        public Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            Guid? repositoryId,
            Guid? workflowId,
            PipelineStatus? status,
            bool allTime,
            CancellationToken ct = default)
        {
            LastPeriodStart = periodStart;
            LastPeriodEnd = periodEnd;
            LastRepositoryId = repositoryId;
            LastWorkflowId = workflowId;
            LastStatus = status;
            LastAllTime = allTime;
            return Task.FromResult(AnalyticsResult);
        }

        public Task<ApiOperationResult<RunDetailsResponse>> GetRunDetailsAsync(
            Guid runId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<RunDetailsResponse>.Failed("Not configured"));

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl, string gitHubProfile, string gitHubToken, CancellationToken ct = default) =>
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
            Task.FromResult(ApiOperationResult<MetricsSummaryResponse>.Failed("Not configured"));

        public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(DashboardLoadResult.Failed("Not configured"));

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
