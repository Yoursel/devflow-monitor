using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Contracts.Security;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevFlowMonitor.Tests;

public class DevFlowApiClientTests
{
    [Fact]
    public async Task CheckConnectionAsync_PostsGitHubSettingsToConfiguredApi()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/github/check-connection"
                ? JsonResponse(new GitHubConnectionResponse("Yoursel", 2))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync(
            "http://localhost:5268",
            "Yoursel",
            "github_pat_test");

        var request = Assert.Single(handler.Requests);
        var body = await ReadJsonBodyAsync<GitHubConnectionRequest>(request);

        Assert.Equal(ConnectionStatus.Connected, result.ConnectionStatus);
        Assert.Contains("Yoursel", result.Message);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://localhost:5268/api/github/check-connection"), request.RequestUri);
        Assert.Equal("test-local-api-key", request.Headers.GetValues(LocalApiAuthentication.HeaderName).Single());
        Assert.Equal("Yoursel", body.ProfileOrOwner);
        Assert.Equal("github_pat_test", body.Token);
    }

    [Fact]
    public async Task CheckConnectionAsync_ReturnsFailedForInvalidApiUrl()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage());
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync(
            "Yoursel",
            "Yoursel",
            "github_pat_test");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("URL API имеет некорректный формат", result.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckConnectionAsync_RejectsHttpForRemoteApi()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage());
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync(
            "http://devflow.example.com",
            "Yoursel",
            "github_pat_test");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("DevFlow Monitor поддерживает только локальный API", result.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckConnectionAsync_RejectsHttpsForRemoteApi()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage());
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync(
            "https://devflow.example.com",
            "Yoursel",
            "github_pat_test");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("DevFlow Monitor поддерживает только локальный API", result.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckConnectionAsync_ReturnsApiErrorMessage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.BadRequest, "Укажите GitHub token"));
        var client = CreateClient(handler);

        var result = await client.CheckConnectionAsync(
            "http://localhost:5268",
            "Yoursel",
            "");

        Assert.Equal(ConnectionStatus.Failed, result.ConnectionStatus);
        Assert.Equal("Укажите GitHub token", result.Message);
    }

    [Fact]
    public async Task GetPipelinesAsync_PostsRequestToConfiguredApi()
    {
        var pipelines = new PagedResponse<PipelineSummaryResponse>(
            [
                new PipelineSummaryResponse(
                    Guid.NewGuid(),
                    "Yoursel/DevFlowMonitor / CI",
                    "develop",
                    PipelineStatus.Running,
                    DateTimeOffset.Parse("2026-06-28T11:15:30Z"),
                    0,
                    0)
            ],
            Page: 2,
            PageSize: 5,
            TotalItems: 12);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/github/pipelines"
                ? JsonResponse(pipelines)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler, Settings());

        var result = await client.GetPipelinesAsync(2, 5);

        var request = Assert.Single(handler.Requests);
        var body = await ReadJsonBodyAsync<GitHubPipelinesRequest>(request);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Items);
        Assert.Equal(12, result.TotalItems);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://localhost:5268/api/github/pipelines"), request.RequestUri);
        Assert.Equal("Yoursel", body.ProfileOrOwner);
        Assert.Equal("github_pat_test", body.Token);
        Assert.Equal(2, body.Page);
        Assert.Equal(5, body.PageSize);
    }

    [Fact]
    public async Task GetPipelinesAsync_ReturnsApiError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.BadRequest, "GitHub отклонил токен доступа"));
        var client = CreateClient(handler, Settings());

        var result = await client.GetPipelinesAsync(1, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("GitHub отклонил токен доступа", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPipelinesAsync_WithActiveAccount_ReadsStoredHistory()
    {
        var accountId = Guid.NewGuid();
        var response = new PagedResponse<PipelineSummaryResponse>([], 1, 5, 0);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == $"/api/github/accounts/{accountId}/pipelines"
                ? JsonResponse(response)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var settings = Settings();
        settings.ActiveGitHubAccountId = accountId;
        var client = CreateClient(handler, settings);

        var result = await client.GetPipelinesAsync(
            1,
            5,
            search: "build & test",
            branch: "feature/demo",
            status: PipelineStatus.Failed);

        var request = Assert.Single(handler.Requests);
        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("search=build%20%26%20test", request.RequestUri!.Query);
        Assert.Contains("branch=feature%2Fdemo", request.RequestUri.Query);
        Assert.Contains("status=Failed", request.RequestUri.Query);
    }

    [Fact]
    public async Task RefreshPipelinesAsync_SynchronizesActiveAccountBeforeReadingStoredHistory()
    {
        var accountId = Guid.NewGuid();
        var pipelines = new PagedResponse<PipelineSummaryResponse>([], 1, 5, 0);
        var synchronizedAt = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            var path when path == $"/api/github/accounts/{accountId}/sync" => JsonResponse(
                new GitHubSyncResponse(accountId, 1, 1, 1, 1, 1, 1, synchronizedAt, [])),
            var path when path == $"/api/github/accounts/{accountId}/pipelines" => JsonResponse(pipelines),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var settings = Settings();
        settings.ActiveGitHubAccountId = accountId;
        settings.GitHubAccounts.Add(new GitHubAccountSettings
        {
            Id = accountId,
            Owner = "Yoursel",
            Token = "active_account_token"
        });
        var client = CreateClient(handler, settings);

        var result = await client.RefreshPipelinesAsync(1, 5, status: PipelineStatus.Running);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal($"/api/github/accounts/{accountId}/sync", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal($"/api/github/accounts/{accountId}/pipelines", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Contains("status=Running", handler.Requests[1].RequestUri!.Query);

        var syncRequest = await ReadJsonBodyAsync<GitHubAccountSyncRequest>(handler.Requests[0]);
        Assert.Equal("active_account_token", syncRequest.Token);
        Assert.False(syncRequest.FullHistory);
    }

    [Fact]
    public async Task GetDashboardAsync_PostsRequestToConfiguredApi()
    {
        var summary = new DashboardSummaryResponse(
            TotalRuns: 3,
            SuccessfulRuns: 1,
            FailedRuns: 1,
            RecentPipelines:
            [
                new PipelineSummaryResponse(
                    Guid.NewGuid(),
                    "Yoursel/DevFlowMonitor / CI",
                    "feature/actions",
                    PipelineStatus.Running,
                    DateTimeOffset.Parse("2026-06-28T11:00:00Z"),
                    0,
                    0)
            ]);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/github/dashboard"
                ? JsonResponse(summary)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler, Settings());

        var result = await client.GetDashboardAsync();

        var request = Assert.Single(handler.Requests);
        var body = await ReadJsonBodyAsync<GitHubConnectionRequest>(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Summary);
        Assert.Equal(3, result.Summary.TotalRuns);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://localhost:5268/api/github/dashboard"), request.RequestUri);
        Assert.Equal("Yoursel", body.ProfileOrOwner);
        Assert.Equal("github_pat_test", body.Token);
    }

    [Fact]
    public async Task GetAnalyticsAsync_SendsPeriodAndScopeFilters()
    {
        var accountId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var from = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var metrics = new MetricsSummaryResponse(from, to, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], []);
        var response = new AnalyticsResponse(from, to, metrics, [], [], []);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == $"/api/github/accounts/{accountId}/analytics"
                ? JsonResponse(response)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var settings = Settings();
        settings.ActiveGitHubAccountId = accountId;
        var client = CreateClient(handler, settings);

        var result = await client.GetAnalyticsAsync(
            from,
            to,
            repositoryId,
            workflowId,
            PipelineStatus.Failed,
            true);

        var request = Assert.Single(handler.Requests);
        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains($"repositoryId={repositoryId}", request.RequestUri!.Query);
        Assert.Contains($"workflowId={workflowId}", request.RequestUri.Query);
        Assert.Contains("status=Failed", request.RequestUri.Query);
        Assert.Contains("allTime=true", request.RequestUri.Query);
        Assert.Contains("from=2026-09-01T00%3A00%3A00", request.RequestUri.Query);
    }

    [Fact]
    public async Task GetRunDetailsAsync_UsesActiveAccountAndStoredRunId()
    {
        var accountId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var response = new RunDetailsResponse(
            runId, 1, 2, "owner/repo", "CI", "Build", "main", PipelineStatus.Success,
            "success", "push", null, null, "owner", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            null, []);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == $"/api/github/accounts/{accountId}/runs/{runId}"
                ? JsonResponse(response)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var settings = Settings();
        settings.ActiveGitHubAccountId = accountId;
        var client = CreateClient(handler, settings);

        var result = await client.GetRunDetailsAsync(runId);

        Assert.True(result.IsSuccess);
        Assert.Equal(runId, result.Value!.Id);
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    private static AppSettings Settings() => new()
    {
        ApiUrl = "http://localhost:5268",
        GitHubProfile = "Yoursel",
        GitHubToken = "github_pat_test"
    };

    private static async Task<T> ReadJsonBodyAsync<T>(HttpRequestMessage request)
    {
        Assert.NotNull(request.Content);
        var json = await request.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private static HttpResponseMessage TextResponse(HttpStatusCode statusCode, string text) =>
        new(statusCode)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/plain")
        };

    private static DevFlowApiClient CreateClient(
        HttpMessageHandler handler,
        AppSettings? settings = null)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        return new DevFlowApiClient(
            httpClient,
            new StubSettingsService(settings),
            new StubLocalApiKeyProvider(),
            NullLogger<DevFlowApiClient>.Instance);
    }

    private sealed class StubLocalApiKeyProvider : ILocalApiKeyProvider
    {
        public string GetApiKey() => "test-local-api-key";
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var capturedRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
                capturedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                capturedRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    request.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            Requests.Add(capturedRequest);
            return responseFactory(request);
        }
    }

    private sealed class StubSettingsService(AppSettings? settings = null) : IAppSettingsService
    {
        public AppSettings Load() => settings ?? new AppSettings();

        public void Save(AppSettings settings)
        {
        }

        public void Update(Action<AppSettings> update)
        {
            update(settings ?? new AppSettings());
        }
    }
}
