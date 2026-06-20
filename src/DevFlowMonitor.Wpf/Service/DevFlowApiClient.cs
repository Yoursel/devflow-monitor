using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Service;

public class DevFlowApiClient(
    HttpClient httpClient,
    IAppSettingsService settingsService,
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

    public async Task<PipelinesLoadResult> GetPipelinesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();

        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return PipelinesLoadResult.Failed(validationError);

        var result = await GetJsonAsync<PagedResponse<PipelineSummaryResponse>>(
            baseUri,
            $"/api/pipelines?page={page}&pageSize={pageSize}",
            "Pipelines",
            "Не удалось загрузить pipelines",
            ct);

        return result.IsSuccess
            ? new PipelinesLoadResult(result.Value!.Items, result.Value.TotalItems)
            : PipelinesLoadResult.Failed(result.ErrorMessage!);
    }

    public async Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default)
    {
        var settings = settingsService.Load();

        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return DashboardLoadResult.Failed(validationError);

        var result = await GetJsonAsync<DashboardSummaryResponse>(
            baseUri,
            "/api/dashboard",
            "Dashboard",
            "Не удалось загрузить dashboard",
            ct);

        return result.IsSuccess
            ? new DashboardLoadResult(result.Value)
            : DashboardLoadResult.Failed(result.ErrorMessage!);
    }

    private async Task<ApiLoadResult<T>> GetJsonAsync<T>(
        Uri baseUri,
        string relativeUrl,
        string operationName,
        string failedMessage,
        CancellationToken ct)
    {
        try
        {
            using var response = await httpClient
                .GetAsync(new Uri(baseUri, relativeUrl), ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{OperationName} request returned HTTP {StatusCode}",
                    operationName,
                    (int)response.StatusCode);

                return ApiLoadResult<T>.Failed(
                    $"API вернул HTTP {(int)response.StatusCode}");
            }

            var result = await response.Content
                .ReadFromJsonAsync<T>(cancellationToken: ct)
                .ConfigureAwait(false);

            return result is null
                ? ApiLoadResult<T>.Failed("API вернул пустой ответ")
                : ApiLoadResult<T>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error while loading {OperationName}", operationName);
            return ApiLoadResult<T>.Failed(failedMessage);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("{OperationName} request timed out", operationName);
            return ApiLoadResult<T>.Failed("Превышено время ожидания (10 сек)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid {OperationName} response", operationName);
            return ApiLoadResult<T>.Failed("API вернул ответ в некорректном формате");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while loading {OperationName}", operationName);
            return ApiLoadResult<T>.Failed(failedMessage);
        }
    }

    private sealed record ApiLoadResult<T>(T? Value, string? ErrorMessage)
    {
        public bool IsSuccess => ErrorMessage is null;

        public static ApiLoadResult<T> Success(T value) =>
            new(value, null);

        public static ApiLoadResult<T> Failed(string message) =>
            new(default, message);
    }

    private static bool TryCreateBaseUri(
        string apiUrl,
        out Uri baseUri,
        out string validationError)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            baseUri = null!;
            validationError = "Сначала укажите URL API в настройках";
            return false;
        }

        if (!Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out baseUri!)
            || baseUri.Scheme is not ("http" or "https"))
        {
            validationError = "URL API имеет некорректный формат";
            return false;
        }

        validationError = string.Empty;
        return true;
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