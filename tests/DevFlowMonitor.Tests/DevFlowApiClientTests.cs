using System.Net;
using System.Net.Http.Json;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevFlowMonitor.Tests;

public class DevFlowApiClientTests
{
    [Fact]
    public async Task CheckConnectionAsync_ReturnsSuccessForHealthyApi()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new HealthResponse(ApiHealthStatus.Healthy, "1.2.3", DateTimeOffset.UtcNow))
            });
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync("http://localhost:5268");

        Assert.Equal(ConnectionStatus.Connected, result.ConnectionStatus);
        Assert.Equal(ApiHealthStatus.Healthy, result.ApiStatus);
        Assert.Equal("1.2.3", result.ApiVersion);
        Assert.Equal(new Uri("http://localhost:5268/api/health"), handler.LastRequestUri);
    }

    [Fact]
    public async Task CheckConnectionAsync_ReturnsFailedForUnsupportedScheme()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage()));

        var result = await client.CheckConnectionAsync("ftp://localhost");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("URL API имеет некорректный формат", result.Message);
    }

    [Fact]
    public async Task CheckConnectionAsync_ReturnsConnectedForDegradedApi()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new HealthResponse(ApiHealthStatus.Degraded, "1.2.3", DateTimeOffset.UtcNow))
            });
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync("http://localhost:5268");

        Assert.Equal(ConnectionStatus.Connected, result.ConnectionStatus);
        Assert.Equal(ApiHealthStatus.Degraded, result.ApiStatus);
        Assert.Contains("ограничениями", result.Message);
    }

    [Fact]
    public async Task CheckConnectionAsync_ReturnsFailedForNetworkError()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException());
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync("http://localhost:5268");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("Не удалось подключиться к API", result.Message);
    }

    [Fact]
    public async Task GetPipelinesAsync_RequestsPageFromConfiguredApi()
    {
        var page = new PagedResponse<PipelineSummaryResponse>(
            [
                new PipelineSummaryResponse(
                    Guid.NewGuid(),
                    "backend-ci",
                    "main",
                    PipelineStatus.Success,
                    DateTimeOffset.UtcNow,
                    12,
                    1)
            ],
            Page: 2,
            PageSize: 5,
            TotalItems: 12);
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(page)
            });
        var client = CreateClient(handler);

        var result = await client.GetPipelinesAsync(2, 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Items);
        Assert.Equal(12, result.TotalItems);
        Assert.Equal(
            new Uri("http://localhost:5268/api/pipelines?page=2&pageSize=5"),
            handler.LastRequestUri);
    }

    [Fact]
    public async Task GetPipelinesAsync_ReturnsErrorForBadRequest()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = CreateClient(handler);

        var result = await client.GetPipelinesAsync(0, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("API вернул HTTP 400", result.ErrorMessage);
    }

    [Fact]
    public async Task GetDashboardAsync_RequestsDashboardFromConfiguredApi()
    {
        var summary = new DashboardSummaryResponse(
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
                    DateTimeOffset.UtcNow,
                    12,
                    1)
            ]);
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(summary)
            });
        var client = CreateClient(handler);

        var result = await client.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Summary);
        Assert.Equal(30, result.Summary.TotalRuns);
        Assert.Equal(
            new Uri("http://localhost:5268/api/dashboard"),
            handler.LastRequestUri);
    }

    private static DevFlowApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        return new DevFlowApiClient(
            httpClient,
            new StubSettingsService(),
            NullLogger<DevFlowApiClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StubSettingsService : IAppSettingsService
    {
        public AppSettings Load() => new()
        {
            ApiUrl = "http://localhost:5268"
        };

        public void Save(AppSettings settings)
        {
        }
    }
}