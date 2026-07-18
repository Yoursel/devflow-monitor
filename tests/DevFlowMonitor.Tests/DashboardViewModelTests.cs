using DevFlowMonitor.Contracts;
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
                    ]))
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
        Assert.Empty(viewModel.StatusMessage);
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

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public DashboardLoadResult DashboardResult { get; init; } =
            DashboardLoadResult.Failed("Not configured");

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl,
            string gitHubProfile,
            string gitHubToken,
            CancellationToken ct = default) =>
            Task.FromResult(new ConnectionCheckResult(ConnectionStatus.Connected, "Connected"));

        public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(DashboardResult);

        public Task<PipelinesLoadResult> GetPipelinesAsync(
            int page,
            int pageSize,
            CancellationToken ct = default) =>
            Task.FromResult(PipelinesLoadResult.Failed("Not configured"));
    }
}
