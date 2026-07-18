using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.GitHub;

internal sealed class GitHubActionsClient(
    HttpClient httpClient,
    ILogger<GitHubActionsClient> logger) : IGitHubActionsClient
{
    public async Task<GitHubActionsResult<GitHubConnectionResponse>> CheckConnectionAsync(
        GitHubConnectionRequest request,
        CancellationToken ct = default)
    {
        if (!TryCreateTarget(request, out var target, out var validationError))
            return GitHubActionsResult<GitHubConnectionResponse>.Failed(validationError);

        var repositoriesResult = await GetRepositoriesAsync(target, request.Token, ct);

        return repositoriesResult.IsSuccess
            ? GitHubActionsResult<GitHubConnectionResponse>.Success(
                new GitHubConnectionResponse(target.Owner, repositoriesResult.Value!.Count))
            : GitHubActionsResult<GitHubConnectionResponse>.Failed(repositoriesResult.ErrorMessage!);
    }

    public async Task<GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>> GetPipelinesAsync(
        GitHubPipelinesRequest request,
        CancellationToken ct = default)
    {
        if (request.Page < 1)
            return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Failed(
                "Page must be greater than or equal to 1.");

        if (request.PageSize is < 1 or > 50)
            return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Failed(
                "PageSize must be between 1 and 50.");

        if (!TryCreateTarget(request.ProfileOrOwner, request.Token, out var target, out var validationError))
            return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Failed(validationError);

        var repositoriesResult = await GetRepositoriesAsync(target, request.Token, ct);

        if (!repositoriesResult.IsSuccess)
            return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Failed(repositoriesResult.ErrorMessage!);

        var runsResult = await GetRunsForRepositoriesAsync(
            repositoriesResult.Value!,
            request.Token,
            perRepository: 100,
            aggregateByWorkflow: true,
            ct);

        if (!runsResult.IsSuccess)
            return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Failed(runsResult.ErrorMessage!);

        var pipelines = runsResult.Value!
            .OrderByDescending(pipeline => pipeline.StartedAt)
            .ToArray();

        var items = pipelines
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();

        return GitHubActionsResult<PagedResponse<PipelineSummaryResponse>>.Success(
            new PagedResponse<PipelineSummaryResponse>(
                items,
                request.Page,
                request.PageSize,
                pipelines.Length));
    }

    public async Task<GitHubActionsResult<DashboardSummaryResponse>> GetDashboardAsync(
        GitHubConnectionRequest request,
        CancellationToken ct = default)
    {
        if (!TryCreateTarget(request, out var target, out var validationError))
            return GitHubActionsResult<DashboardSummaryResponse>.Failed(validationError);

        var repositoriesResult = await GetRepositoriesAsync(target, request.Token, ct);

        if (!repositoriesResult.IsSuccess)
            return GitHubActionsResult<DashboardSummaryResponse>.Failed(repositoriesResult.ErrorMessage!);

        var runsResult = await GetRunsForRepositoriesAsync(
            repositoriesResult.Value!,
            request.Token,
            perRepository: 20,
            aggregateByWorkflow: false,
            ct);

        if (!runsResult.IsSuccess)
            return GitHubActionsResult<DashboardSummaryResponse>.Failed(runsResult.ErrorMessage!);

        var pipelines = runsResult.Value!
            .OrderByDescending(pipeline => pipeline.StartedAt)
            .ToArray();

        return GitHubActionsResult<DashboardSummaryResponse>.Success(new DashboardSummaryResponse(
            TotalRuns: pipelines.Length,
            SuccessfulRuns: pipelines.Count(pipeline => pipeline.Status == PipelineStatus.Success),
            FailedRuns: pipelines.Count(pipeline => pipeline.Status == PipelineStatus.Failed),
            RecentPipelines: pipelines.Take(4).ToArray()));
    }

    private async Task<GitHubActionsResult<IReadOnlyList<GitHubRepository>>> GetRepositoriesAsync(
        GitHubTarget target,
        string token,
        CancellationToken ct)
    {
        var repositories = new List<GitHubRepository>();

        for (var page = 1; page <= 5; page++)
        {
            var result = await GetGitHubJsonAsync<IReadOnlyList<GitHubRepositoryResponse>>(
                $"user/repos?visibility=all&affiliation=owner,collaborator,organization_member&per_page=100&page={page}",
                token,
                "GitHub repositories",
                "Не удалось загрузить репозитории GitHub",
                ct);

            if (!result.IsSuccess)
                return GitHubActionsResult<IReadOnlyList<GitHubRepository>>.Failed(result.ErrorMessage!);

            var pageRepositories = result.Value!
                .Where(repository =>
                    repository is { Archived: false, Disabled: false }
                    && string.Equals(repository.Owner.Login, target.Owner, StringComparison.OrdinalIgnoreCase))
                .Select(repository => new GitHubRepository(
                    Uri.EscapeDataString(repository.Owner.Login),
                    Uri.EscapeDataString(repository.Name),
                    repository.FullName))
                .ToArray();

            repositories.AddRange(pageRepositories);

            if (result.Value!.Count < 100)
                break;
        }

        return repositories.Count == 0
            ? GitHubActionsResult<IReadOnlyList<GitHubRepository>>.Failed(
                $"GitHub не вернул доступных репозиториев для {target.Owner}")
            : GitHubActionsResult<IReadOnlyList<GitHubRepository>>.Success(repositories);
    }

    private async Task<GitHubActionsResult<IReadOnlyList<PipelineSummaryResponse>>> GetRunsForRepositoriesAsync(
        IReadOnlyList<GitHubRepository> repositories,
        string token,
        int perRepository,
        bool aggregateByWorkflow,
        CancellationToken ct)
    {
        var pipelines = new List<PipelineSummaryResponse>();
        var successfulRepositories = 0;
        string? firstError = null;

        foreach (var repository in repositories)
        {
            var result = await GetGitHubJsonAsync<GitHubWorkflowRunsResponse>(
                $"repos/{repository.Owner}/{repository.Name}/actions/runs?per_page={perRepository}&page=1",
                token,
                "GitHub workflow runs",
                "Не удалось загрузить GitHub Actions",
                ct);

            if (!result.IsSuccess)
            {
                firstError ??= result.ErrorMessage;
                logger.LogWarning(
                    "Skipping GitHub Actions runs for {Repository}: {ErrorMessage}",
                    repository.FullName,
                    result.ErrorMessage);
                continue;
            }

            successfulRepositories++;

            pipelines.AddRange(aggregateByWorkflow
                ? AggregateRuns(repository, result.Value!.WorkflowRuns)
                : result.Value!.WorkflowRuns.Select(run => MapWorkflow(repository, [run])));
        }

        return successfulRepositories == 0
            ? GitHubActionsResult<IReadOnlyList<PipelineSummaryResponse>>.Failed(
                firstError ?? "Не удалось загрузить GitHub Actions")
            : GitHubActionsResult<IReadOnlyList<PipelineSummaryResponse>>.Success(pipelines);
    }

    private async Task<GitHubActionsResult<T>> GetGitHubJsonAsync<T>(
        string relativeUrl,
        string token,
        string operationName,
        string failedMessage,
        CancellationToken ct)
    {
        try
        {
            using var request = CreateGitHubRequest(relativeUrl, token);
            using var response = await httpClient
                .SendAsync(request, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{OperationName} request returned HTTP {StatusCode}",
                    operationName,
                    (int)response.StatusCode);

                return GitHubActionsResult<T>.Failed(
                    CreateGitHubHttpErrorMessage((int)response.StatusCode));
            }

            var result = await response.Content
                .ReadFromJsonAsync<T>(cancellationToken: ct)
                .ConfigureAwait(false);

            return result is null
                ? GitHubActionsResult<T>.Failed("GitHub вернул пустой ответ")
                : GitHubActionsResult<T>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error while loading {OperationName}", operationName);
            return GitHubActionsResult<T>.Failed(failedMessage);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("{OperationName} request timed out", operationName);
            return GitHubActionsResult<T>.Failed("Превышено время ожидания (10 сек)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid {OperationName} response", operationName);
            return GitHubActionsResult<T>.Failed("GitHub вернул ответ в некорректном формате");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while loading {OperationName}", operationName);
            return GitHubActionsResult<T>.Failed(failedMessage);
        }
    }

    private static HttpRequestMessage CreateGitHubRequest(
        string relativeUrl,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("DevFlowMonitor");

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        return request;
    }

    internal static IReadOnlyList<PipelineSummaryResponse> AggregateRuns(
        GitHubRepository repository,
        IReadOnlyList<GitHubWorkflowRun> runs) =>
        runs
            .GroupBy(run => GetWorkflowKey(run), StringComparer.OrdinalIgnoreCase)
            .Select(group => MapWorkflow(repository, group))
            .OrderByDescending(pipeline => pipeline.StartedAt)
            .ToArray();

    private static PipelineSummaryResponse MapWorkflow(
        GitHubRepository repository,
        IEnumerable<GitHubWorkflowRun> workflowRuns)
    {
        var runs = workflowRuns
            .OrderByDescending(run => run.RunStartedAt ?? run.CreatedAt ?? DateTimeOffset.MinValue)
            .ToArray();
        var latestRun = runs[0];
        var latestStatus = MapStatus(latestRun.Status, latestRun.Conclusion);
        var workflowName = runs
            .Select(run => FirstNotEmpty(run.Name))
            .FirstOrDefault(name => name is not null && !LooksLikeWorkflowPath(name))
            ?? FirstNotEmpty(latestRun.Name, latestRun.DisplayTitle)
            ?? $"workflow-{latestRun.WorkflowId}";

        return new PipelineSummaryResponse(
            Id: CreatePipelineId(latestRun.Id),
            PipelineName: $"{repository.FullName} / {workflowName}",
            Branch: FirstNotEmpty(latestRun.HeadBranch) ?? "-",
            Status: latestStatus,
            StartedAt: latestRun.RunStartedAt ?? latestRun.CreatedAt ?? DateTimeOffset.UtcNow,
            SuccessfulRuns: runs.Count(run => MapStatus(run.Status, run.Conclusion) == PipelineStatus.Success),
            FailedRuns: runs.Count(run => MapStatus(run.Status, run.Conclusion) == PipelineStatus.Failed));
    }

    private static string GetWorkflowKey(GitHubWorkflowRun run) =>
        run.WorkflowId > 0
            ? run.WorkflowId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : FirstNotEmpty(run.Name) ?? $"run-{run.Id}";

    private static bool LooksLikeWorkflowPath(string value) =>
        value.Contains('/')
        || value.Contains('\\')
        || value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    private static PipelineStatus MapStatus(string? status, string? conclusion)
    {
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            return PipelineStatus.Running;

        return conclusion?.Trim().ToLowerInvariant() switch
        {
            "success" => PipelineStatus.Success,
            "cancelled" or "skipped" or "neutral" => PipelineStatus.Cancelled,
            _ => PipelineStatus.Failed
        };
    }

    private static Guid CreatePipelineId(long runId)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(runId).CopyTo(bytes, 0);

        return new Guid(bytes);
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string CreateGitHubHttpErrorMessage(int statusCode) =>
        statusCode switch
        {
            401 => "GitHub отклонил токен доступа",
            403 => "Нет доступа к GitHub Actions или превышен лимит запросов",
            404 => "GitHub репозиторий не найден или нет доступа",
            _ => $"GitHub вернул HTTP {statusCode}"
        };

    private static bool TryCreateTarget(
        GitHubConnectionRequest request,
        out GitHubTarget target,
        out string validationError) =>
        TryCreateTarget(request.ProfileOrOwner, request.Token, out target, out validationError);

    private static bool TryCreateTarget(
        string profileOrOwner,
        string token,
        out GitHubTarget target,
        out string validationError)
    {
        var owner = GetGitHubOwner(profileOrOwner);

        if (string.IsNullOrWhiteSpace(owner))
        {
            target = null!;
            validationError = "Укажите GitHub профиль или организацию";
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            target = null!;
            validationError = "Укажите GitHub token";
            return false;
        }

        target = new GitHubTarget(owner);
        validationError = string.Empty;
        return true;
    }

    private static string GetGitHubOwner(string profileOrOwner)
    {
        if (string.IsNullOrWhiteSpace(profileOrOwner))
            return string.Empty;

        var value = profileOrOwner.Trim().TrimStart('@').TrimEnd('/');

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return IsGitHubHost(uri.Host)
                ? GetFirstPathSegment(uri.AbsolutePath)
                : string.Empty;

        if (value.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            value = value["github.com/".Length..];
        else if (value.StartsWith("www.github.com/", StringComparison.OrdinalIgnoreCase))
            value = value["www.github.com/".Length..];

        return GetFirstPathSegment(value);
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);

    private static string GetFirstPathSegment(string value) =>
        value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}
