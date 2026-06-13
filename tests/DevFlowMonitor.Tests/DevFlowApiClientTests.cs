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

    private static DevFlowApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        return new DevFlowApiClient(
            httpClient,
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
}
