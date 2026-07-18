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
        string gitHubProfile,
        string gitHubToken,
        CancellationToken ct = default)
    {
        if (!TryCreateBaseUri(apiUrl, out var baseUri, out var validationError))
            return Failed(validationError);

        var result = await PostApiJsonAsync<GitHubConnectionResponse>(
            baseUri,
            "/api/github/check-connection",
            new GitHubConnectionRequest(gitHubProfile, gitHubToken),
            "GitHub connection check",
            "Не удалось проверить подключение к GitHub через API",
            ct);

        if (!result.IsSuccess)
            return Failed(result.ErrorMessage!);

        return new ConnectionCheckResult(
            ConnectionStatus.Connected,
            $"Соединение установлено. GitHub: {result.Value!.Owner}, репозиториев: {result.Value.RepositoryCount}");
    }

    public async Task<PipelinesLoadResult> GetPipelinesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();

        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return PipelinesLoadResult.Failed(validationError);

        var result = await PostApiJsonAsync<PagedResponse<PipelineSummaryResponse>>(
            baseUri,
            "/api/github/pipelines",
            new GitHubPipelinesRequest(settings.GitHubProfile, settings.GitHubToken, page, pageSize),
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

        var result = await PostApiJsonAsync<DashboardSummaryResponse>(
            baseUri,
            "/api/github/dashboard",
            new GitHubConnectionRequest(settings.GitHubProfile, settings.GitHubToken),
            "Dashboard",
            "Не удалось загрузить dashboard",
            ct);

        return result.IsSuccess
            ? new DashboardLoadResult(result.Value)
            : DashboardLoadResult.Failed(result.ErrorMessage!);
    }

    private async Task<ApiLoadResult<T>> PostApiJsonAsync<T>(
        Uri baseUri,
        string relativeUrl,
        object requestBody,
        string operationName,
        string failedMessage,
        CancellationToken ct)
    {
        try
        {
            using var response = await httpClient
                .PostAsJsonAsync(new Uri(baseUri, relativeUrl), requestBody, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{OperationName} request returned HTTP {StatusCode}",
                    operationName,
                    (int)response.StatusCode);

                var errorMessage = await response.Content
                    .ReadAsStringAsync(ct)
                    .ConfigureAwait(false);

                return ApiLoadResult<T>.Failed(
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? $"API вернул HTTP {(int)response.StatusCode}"
                        : errorMessage);
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

        if (baseUri.Scheme == Uri.UriSchemeHttp && !baseUri.IsLoopback)
        {
            validationError = "Для удалённого API необходимо использовать HTTPS";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static ConnectionCheckResult Failed(string message) =>
        new(ConnectionStatus.Failed, message);

    private sealed record ApiLoadResult<T>(T? Value, string? ErrorMessage)
    {
        public bool IsSuccess => ErrorMessage is null;

        public static ApiLoadResult<T> Success(T value) =>
            new(value, null);

        public static ApiLoadResult<T> Failed(string message) =>
            new(default, message);
    }
}
