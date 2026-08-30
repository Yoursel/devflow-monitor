using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Tests;

public class PipelinesListViewModelTests
{
    [Fact]
    public async Task LoadAsync_MapsPipelinesAndPagination()
    {
        var apiClient = new StubApiClient
        {
            Result = new PipelinesLoadResult(
                [
                    new PipelineSummaryResponse(
                        Guid.NewGuid(),
                        "backend-ci",
                        "main",
                        PipelineStatus.Success,
                        DateTimeOffset.UtcNow.AddMinutes(-5),
                        12,
                        1,
                        Runs:
                        [
                            new PipelineRunResponse(
                                42, 17, "Fix build", "main", PipelineStatus.Success,
                                DateTimeOffset.UtcNow.AddMinutes(-5))
                        ])
                ],
                TotalItems: 12)
        };
        var viewModel = new PipelinesListViewModel(apiClient);

        await viewModel.LoadAsync();

        var pipeline = Assert.Single(viewModel.Pipelines);
        Assert.Equal("backend-ci", pipeline.PipelineName);
        Assert.Equal(12, pipeline.SuccessfulRuns);
        Assert.Equal("Показано 1-5 из 12", viewModel.Pagination.ItemRangeInfo);
        Assert.Empty(viewModel.StatusMessage);
    }

    [Fact]
    public async Task HistoryCommands_OpenSelectedPipelineAndReturnToList()
    {
        var apiClient = new StubApiClient
        {
            Result = new PipelinesLoadResult(
                [new PipelineSummaryResponse(
                    Guid.NewGuid(), "backend-ci", "main", PipelineStatus.Success,
                    DateTimeOffset.UtcNow, 1, 0, Runs:
                    [new PipelineRunResponse(42, 17, "Fix build", "main", PipelineStatus.Success, DateTimeOffset.UtcNow)])],
                1)
        };
        var viewModel = new PipelinesListViewModel(apiClient);

        await viewModel.LoadAsync();
        var pipeline = Assert.Single(viewModel.Pipelines);
        pipeline.OpenHistoryCommand.Execute(null);

        Assert.Same(pipeline, viewModel.SelectedPipeline);

        viewModel.CloseHistoryCommand.Execute(null);

        Assert.Null(viewModel.SelectedPipeline);
    }

    [Fact]
    public async Task LoadAsync_ShowsClientError()
    {
        var viewModel = new PipelinesListViewModel(new StubApiClient
        {
            Result = PipelinesLoadResult.Failed("Сначала укажите URL API в настройках")
        });

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Pipelines);
        Assert.Equal("Сначала укажите URL API в настройках", viewModel.StatusMessage);
    }

    [Fact]
    public async Task PaginationCommands_LoadNextAndPreviousPages()
    {
        var apiClient = new StubApiClient
        {
            ResultsByPage =
            {
                [1] = Page("backend-ci"),
                [2] = Page("frontend-deploy")
            }
        };
        var viewModel = new PipelinesListViewModel(apiClient);

        await viewModel.LoadAsync();
        viewModel.Pagination.GoToNextPageCommand.Execute(null);
        await WaitUntil(() => apiClient.RequestedPages.SequenceEqual([1, 2]));

        Assert.Equal("frontend-deploy", Assert.Single(viewModel.Pipelines).PipelineName);

        viewModel.Pagination.GoToPreviousPageCommand.Execute(null);
        await WaitUntil(() => apiClient.RequestedPages.SequenceEqual([1, 2, 1]));

        Assert.Equal("backend-ci", Assert.Single(viewModel.Pipelines).PipelineName);
    }

    [Fact]
    public async Task ApplyFilters_ForwardsValuesAndReturnsToFirstPage()
    {
        var apiClient = new StubApiClient { Result = Page("filtered") };
        var viewModel = new PipelinesListViewModel(apiClient)
        {
            SearchText = "backend",
            BranchFilter = "main",
            SelectedStatus = PipelineStatus.Success
        };

        viewModel.ApplyFiltersCommand.Execute(null);
        await WaitUntil(() => apiClient.Requests.Count == 1);

        var request = Assert.Single(apiClient.Requests);
        Assert.Equal(1, request.Page);
        Assert.Equal("backend", request.Search);
        Assert.Equal("main", request.Branch);
        Assert.Equal(PipelineStatus.Success, request.Status);
    }

    private static PipelinesLoadResult Page(string pipelineName) =>
        new(
            [
                new PipelineSummaryResponse(
                    Guid.NewGuid(),
                    pipelineName,
                    "main",
                    PipelineStatus.Success,
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    12,
                    1)
            ],
            TotalItems: 10);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public PipelinesLoadResult Result { get; init; } =
            new([], 0);

        public Dictionary<int, PipelinesLoadResult> ResultsByPage { get; } = [];
        public List<int> RequestedPages { get; } = [];
        public List<(int Page, string? Search, string? Branch, PipelineStatus? Status)> Requests { get; } = [];

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl,
            string gitHubProfile,
            string gitHubToken,
            CancellationToken ct = default) =>
            Task.FromResult(new ConnectionCheckResult(ConnectionStatus.Connected, "Connected"));

        public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(DashboardLoadResult.Failed("Not configured"));

        public Task<PipelinesLoadResult> GetPipelinesAsync(
            int page,
            int pageSize,
            string? search = null,
            string? branch = null,
            PipelineStatus? status = null,
            CancellationToken ct = default)
        {
            RequestedPages.Add(page);
            Requests.Add((page, search, branch, status));
            return Task.FromResult(ResultsByPage.GetValueOrDefault(page, Result));
        }
    }
}
