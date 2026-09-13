using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.GitHub;

internal sealed class GitHubActionsClient(
    IGitHubApiClient apiClient,
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

        var pipelines = ApplyFilters(runsResult.Value!, request)
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

    internal static IEnumerable<PipelineSummaryResponse> ApplyFilters(
        IEnumerable<PipelineSummaryResponse> pipelines,
        GitHubPipelinesRequest request)
    {
        var filtered = pipelines;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(pipeline =>
                pipeline.PipelineName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Branch))
        {
            var branch = request.Branch.Trim();
            filtered = filtered.Where(pipeline =>
                pipeline.Branch.Contains(branch, StringComparison.OrdinalIgnoreCase));
        }

        if (request.Status is { } status)
            filtered = filtered.Where(pipeline => pipeline.Status == status);

        return filtered;
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
        List<GitHubRepository> repositories = [];

        for (var page = 1; page <= 5; page++)
        {
            var result = await apiClient.GetAsync<IReadOnlyList<GitHubRepositoryResponse>>(
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
        List<PipelineSummaryResponse> pipelines = [];
        var successfulRepositories = 0;
        string? firstError = null;

        foreach (var repository in repositories)
        {
            var result = await apiClient.GetAsync<GitHubWorkflowRunsResponse>(
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
        var latestStatus = GitHubRunOutcome.ToPipelineStatus(latestRun.Status, latestRun.Conclusion);
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
            SuccessfulRuns: runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Success),
            FailedRuns: runs.Count(run => GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion) == PipelineStatus.Failed),
            Runs: runs.Select(MapRun).ToArray());
    }

    private static PipelineRunResponse MapRun(GitHubWorkflowRun run) =>
        new(
            run.Id,
            run.RunNumber,
            FirstNotEmpty(run.DisplayTitle, run.Name) ?? $"Run {run.Id}",
            FirstNotEmpty(run.HeadBranch) ?? "-",
            GitHubRunOutcome.ToPipelineStatus(run.Status, run.Conclusion),
            run.RunStartedAt ?? run.CreatedAt ?? DateTimeOffset.UtcNow);

    private static string GetWorkflowKey(GitHubWorkflowRun run) =>
        run.WorkflowId > 0
            ? run.WorkflowId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : FirstNotEmpty(run.Name) ?? $"run-{run.Id}";

    private static bool LooksLikeWorkflowPath(string value) =>
        value.Contains('/')
        || value.Contains('\\')
        || value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    private static Guid CreatePipelineId(long runId)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(runId).CopyTo(bytes, 0);

        return new Guid(bytes);
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

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
        var owner = GitHubOwnerParser.Parse(profileOrOwner);

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

}
