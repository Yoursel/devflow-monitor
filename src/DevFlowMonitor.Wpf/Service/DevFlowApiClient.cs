using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Contracts.Security;
using DevFlowMonitor.Wpf.Model;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Service;

public class DevFlowApiClient(
    HttpClient httpClient,
    IAppSettingsService settingsService,
    ILocalApiKeyProvider localApiKeyProvider,
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

    public async Task<ApiOperationResult<GitHubAccountResponse>> AddGitHubAccountAsync(
        string apiUrl,
        string gitHubProfile,
        string gitHubToken,
        CancellationToken ct = default)
    {
        if (!TryCreateBaseUri(apiUrl, out var baseUri, out var validationError))
            return ApiOperationResult<GitHubAccountResponse>.Failed(validationError);

        return await PostApiJsonAsync<GitHubAccountResponse>(
            baseUri,
            "/api/github/accounts",
            new CreateGitHubAccountRequest(gitHubProfile, gitHubToken),
            "Add GitHub account",
            "Не удалось добавить GitHub-аккаунт",
            ct);
    }

    public async Task<ApiOperationResult<GitHubSyncResponse>> SynchronizeGitHubAccountAsync(
        string apiUrl,
        Guid accountId,
        string gitHubToken,
        bool fullHistory = true,
        CancellationToken ct = default)
    {
        if (!TryCreateBaseUri(apiUrl, out var baseUri, out var validationError))
            return ApiOperationResult<GitHubSyncResponse>.Failed(validationError);

        return await PostApiJsonAsync<GitHubSyncResponse>(
            baseUri,
            $"/api/github/accounts/{accountId}/sync",
            new GitHubAccountSyncRequest(gitHubToken, fullHistory),
            "Synchronize GitHub account",
            "Не удалось синхронизировать GitHub-аккаунт",
            ct);
    }

    public async Task<ApiOperationResult<bool>> DeleteGitHubAccountAsync(
        string apiUrl,
        Guid accountId,
        CancellationToken ct = default)
    {
        if (!TryCreateBaseUri(apiUrl, out var baseUri, out var validationError))
            return ApiOperationResult<bool>.Failed(validationError);

        return await SendApiRequestAsync(
            baseUri,
            $"/api/github/accounts/{accountId}",
            HttpMethod.Delete,
            requestBody: null,
            "Delete GitHub account",
            "Не удалось удалить GitHub-аккаунт",
            static () => true,
            ct);
    }

    public Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(CancellationToken ct = default)
    {
        var periodEnd = DateTimeOffset.UtcNow;
        return GetMetricsAsync(periodEnd.AddDays(-30), periodEnd, ct);
    }

    public async Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();
        if (settings.ActiveGitHubAccountId is not { } accountId || accountId == Guid.Empty)
            return ApiOperationResult<MetricsSummaryResponse>.Failed(
                "Добавьте GitHub-аккаунт в настройках и выполните синхронизацию");
        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return ApiOperationResult<MetricsSummaryResponse>.Failed(validationError);

        return await GetApiJsonAsync<MetricsSummaryResponse>(
            baseUri,
            $"/api/github/accounts/{accountId}/metrics?from={Uri.EscapeDataString(periodStart.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(periodEnd.ToString("O", CultureInfo.InvariantCulture))}",
            "Metrics",
            "Не удалось загрузить метрики",
            ct);
    }

    public async Task<ApiOperationResult<IReadOnlyList<RepositoryResponse>>> GetRepositoriesAsync(
        CancellationToken ct = default)
    {
        if (!TryGetActiveAccount(out var baseUri, out var accountId, out var validationError))
            return ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Failed(validationError);

        return await GetApiJsonAsync<IReadOnlyList<RepositoryResponse>>(
            baseUri,
            $"/api/github/accounts/{accountId}/repositories",
            "Repositories",
            "Не удалось загрузить репозитории",
            ct);
    }

    public async Task<ApiOperationResult<IReadOnlyList<WorkflowResponse>>> GetWorkflowsAsync(
        Guid repositoryId,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();
        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return ApiOperationResult<IReadOnlyList<WorkflowResponse>>.Failed(validationError);

        return await GetApiJsonAsync<IReadOnlyList<WorkflowResponse>>(
            baseUri,
            $"/api/repositories/{repositoryId}/workflows",
            "Workflows",
            "Не удалось загрузить пайплайны",
            ct);
    }

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

    public async Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? repositoryId,
        Guid? workflowId,
        PipelineStatus? status,
        bool allTime,
        CancellationToken ct = default)
    {
        if (!TryGetActiveAccount(out var baseUri, out var accountId, out var validationError))
            return ApiOperationResult<AnalyticsResponse>.Failed(validationError);

        List<string> parameters =
        [
            $"from={Uri.EscapeDataString(periodStart.ToString("O", CultureInfo.InvariantCulture))}",
            $"to={Uri.EscapeDataString(periodEnd.ToString("O", CultureInfo.InvariantCulture))}"
        ];
        if (repositoryId.HasValue)
            parameters.Add($"repositoryId={repositoryId}");
        if (workflowId.HasValue)
            parameters.Add($"workflowId={workflowId}");
        if (status.HasValue)
            parameters.Add($"status={status}");
        if (allTime)
            parameters.Add("allTime=true");

        return await GetApiJsonAsync<AnalyticsResponse>(
            baseUri,
            $"/api/github/accounts/{accountId}/analytics?{string.Join('&', parameters)}",
            "Analytics",
            "Не удалось загрузить аналитику",
            ct);
    }

    public async Task<ApiOperationResult<RunDetailsResponse>> GetRunDetailsAsync(
        Guid runId,
        CancellationToken ct = default)
    {
        if (!TryGetActiveAccount(out var baseUri, out var accountId, out var validationError))
            return ApiOperationResult<RunDetailsResponse>.Failed(validationError);

        return await GetApiJsonAsync<RunDetailsResponse>(
            baseUri,
            $"/api/github/accounts/{accountId}/runs/{runId}",
            "Run details",
            "Не удалось загрузить сведения о запуске",
            ct);
    }

    public async Task<PipelinesLoadResult> GetPipelinesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? branch = null,
        PipelineStatus? status = null,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();

        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return PipelinesLoadResult.Failed(validationError);

        if (settings.ActiveGitHubAccountId is { } accountId && accountId != Guid.Empty)
        {
            var parameters = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search))
                parameters.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(branch))
                parameters.Add($"branch={Uri.EscapeDataString(branch)}");
            if (status.HasValue)
                parameters.Add($"status={Uri.EscapeDataString(status.Value.ToString())}");

            var storedResult = await GetApiJsonAsync<PagedResponse<PipelineSummaryResponse>>(
                baseUri,
                $"/api/github/accounts/{accountId}/pipelines?{string.Join('&', parameters)}",
                "Stored pipelines",
                "Не удалось загрузить сохранённые пайплайны",
                ct);
            return storedResult.IsSuccess
                ? new PipelinesLoadResult(storedResult.Value!.Items, storedResult.Value.TotalItems)
                : PipelinesLoadResult.Failed(storedResult.ErrorMessage!);
        }

        var result = await PostApiJsonAsync<PagedResponse<PipelineSummaryResponse>>(
            baseUri,
            "/api/github/pipelines",
            new GitHubPipelinesRequest(
                settings.GitHubProfile,
                settings.GitHubToken,
                page,
                pageSize,
                search,
                branch,
                status),
            "Pipelines",
            "Не удалось загрузить пайплайны",
            ct);

        return result.IsSuccess
            ? new PipelinesLoadResult(result.Value!.Items, result.Value.TotalItems)
            : PipelinesLoadResult.Failed(result.ErrorMessage!);
    }

    public async Task<PipelinesLoadResult> RefreshPipelinesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? branch = null,
        PipelineStatus? status = null,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();
        if (settings.ActiveGitHubAccountId is { } accountId && accountId != Guid.Empty)
        {
            var accountToken = settings.GitHubAccounts
                .FirstOrDefault(account => account.Id == accountId)
                ?.Token;
            var token = string.IsNullOrWhiteSpace(accountToken)
                ? settings.GitHubToken
                : accountToken;

            if (string.IsNullOrWhiteSpace(token))
                return PipelinesLoadResult.Failed("Укажите токен GitHub для обновления пайплайнов");

            var synchronization = await SynchronizeGitHubAccountAsync(
                settings.ApiUrl,
                accountId,
                token,
                fullHistory: false,
                ct);
            if (!synchronization.IsSuccess)
                return PipelinesLoadResult.Failed(synchronization.ErrorMessage!);
        }

        return await GetPipelinesAsync(page, pageSize, search, branch, status, ct);
    }

    public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default)
    {
        var periodEnd = DateTimeOffset.UtcNow;
        return GetDashboardAsync(periodEnd.AddDays(-30), periodEnd, ct);
    }

    public async Task<DashboardLoadResult> GetDashboardAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default)
    {
        var settings = settingsService.Load();

        if (!TryCreateBaseUri(settings.ApiUrl, out var baseUri, out var validationError))
            return DashboardLoadResult.Failed(validationError);

        if (settings.ActiveGitHubAccountId is { } accountId && accountId != Guid.Empty)
        {
            var storedResult = await GetApiJsonAsync<DashboardSummaryResponse>(
                baseUri,
                $"/api/github/accounts/{accountId}/dashboard?from={Uri.EscapeDataString(periodStart.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(periodEnd.ToString("O", CultureInfo.InvariantCulture))}",
                "Stored dashboard",
                "Не удалось загрузить сохранённый dashboard",
                ct);
            return storedResult.IsSuccess
                ? new DashboardLoadResult(storedResult.Value)
                : DashboardLoadResult.Failed(storedResult.ErrorMessage!);
        }

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

    private Task<ApiOperationResult<T>> PostApiJsonAsync<T>(
        Uri baseUri,
        string relativeUrl,
        object requestBody,
        string operationName,
        string failedMessage,
        CancellationToken ct)
    {
        return SendApiRequestAsync<T>(
            baseUri,
            relativeUrl,
            HttpMethod.Post,
            requestBody,
            operationName,
            failedMessage,
            successValueFactory: null,
            ct);
    }

    private Task<ApiOperationResult<T>> GetApiJsonAsync<T>(
        Uri baseUri,
        string relativeUrl,
        string operationName,
        string failedMessage,
        CancellationToken ct)
    {
        return SendApiRequestAsync<T>(
            baseUri,
            relativeUrl,
            HttpMethod.Get,
            requestBody: null,
            operationName,
            failedMessage,
            successValueFactory: null,
            ct);
    }

    private async Task<ApiOperationResult<T>> SendApiRequestAsync<T>(
        Uri baseUri,
        string relativeUrl,
        HttpMethod method,
        object? requestBody,
        string operationName,
        string failedMessage,
        Func<T>? successValueFactory,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(baseUri, relativeUrl));
            request.Headers.Add(
                LocalApiAuthentication.HeaderName,
                localApiKeyProvider.GetApiKey());
            if (requestBody is not null)
                request.Content = JsonContent.Create(requestBody);

            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{OperationName} request returned HTTP {StatusCode}",
                    operationName,
                    (int)response.StatusCode);
                return ApiOperationResult<T>.Failed(await ReadErrorMessageAsync(response, ct));
            }

            if (successValueFactory is not null)
                return ApiOperationResult<T>.Success(successValueFactory());

            var result = await response.Content
                .ReadFromJsonAsync<T>(cancellationToken: ct)
                .ConfigureAwait(false);
            return result is null
                ? ApiOperationResult<T>.Failed("API вернул пустой ответ")
                : ApiOperationResult<T>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error while loading {OperationName}", operationName);
            return ApiOperationResult<T>.Failed(failedMessage);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("{OperationName} request timed out", operationName);
            return ApiOperationResult<T>.Failed("Превышено время ожидания ответа API");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid {OperationName} response", operationName);
            return ApiOperationResult<T>.Failed("API вернул ответ в некорректном формате");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(error))
            return $"API вернул HTTP {(int)response.StatusCode}";

        try
        {
            return JsonSerializer.Deserialize<string>(error) ?? error;
        }
        catch (JsonException)
        {
            return error;
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

        if (!baseUri.IsLoopback)
        {
            validationError = "DevFlow Monitor поддерживает только локальный API";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private bool TryGetActiveAccount(
        out Uri baseUri,
        out Guid accountId,
        out string validationError)
    {
        var settings = settingsService.Load();
        if (settings.ActiveGitHubAccountId is not { } activeAccountId || activeAccountId == Guid.Empty)
        {
            baseUri = null!;
            accountId = Guid.Empty;
            validationError = "Сначала добавьте GitHub-аккаунт";
            return false;
        }

        accountId = activeAccountId;
        return TryCreateBaseUri(settings.ApiUrl, out baseUri, out validationError);
    }

    private static ConnectionCheckResult Failed(string message) =>
        new(ConnectionStatus.Failed, message);

}
