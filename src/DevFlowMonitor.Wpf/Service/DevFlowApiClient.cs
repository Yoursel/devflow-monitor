using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Service;

public class DevFlowApiClient(
    HttpClient httpClient,
    ILogger<DevFlowApiClient> logger)
    : IDevFlowApiClient
{
    public async Task<ConnectionCheckResult> CheckConnectionAsync(
        string apiUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return Failed("URL API не задан");

        if (!Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
            return Failed("URL API имеет некорректный формат");

        try
        {
            using var response = await httpClient
                .GetAsync(new Uri(baseUri, "/api/health"), ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "API connection check returned HTTP {StatusCode} from {ApiUrl}",
                    (int)response.StatusCode,
                    apiUrl);

                return Failed($"API вернул HTTP {(int)response.StatusCode}");
            }

            var health = await response.Content
                .ReadFromJsonAsync<HealthResponse>(cancellationToken: ct)
                .ConfigureAwait(false);

            if (health is null)
                return Failed("API вернул пустой ответ");

            logger.LogInformation(
                "API connection check completed for {ApiUrl}: {ApiStatus}, version {ApiVersion}",
                apiUrl,
                health.Status,
                health.Version);

            return health.Status switch
            {
                ApiHealthStatus.Healthy => Connected(
                    $"Соединение установлено. API v{health.Version}",
                    health),
                ApiHealthStatus.Degraded => Connected(
                    "Соединение установлено, но API работает с ограничениями",
                    health),
                ApiHealthStatus.Unhealthy => Connected(
                    "Соединение установлено, но API не готово к работе",
                    health),
                _ => Failed("API вернул неизвестный статус", health)
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error during connection check to {ApiUrl}", apiUrl);
            return Failed("Не удалось подключиться к API");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("API connection check timed out for {ApiUrl}", apiUrl);
            return Failed("Превышено время ожидания (10 сек)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid health response received from {ApiUrl}", apiUrl);
            return Failed("API вернул ответ в некорректном формате");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during connection check to {ApiUrl}", apiUrl);
            return Failed("Не удалось проверить подключение к API");
        }
    }

    private static ConnectionCheckResult Failed(string message, HealthResponse? health = null) =>
        new(
            ConnectionStatus.Failed,
            message,
            health?.Status,
            health?.Version);

    private static ConnectionCheckResult Connected(string message, HealthResponse health) =>
        new(
            ConnectionStatus.Connected,
            message,
            health.Status,
            health.Version);
}
