using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.Data.Entities;
using DevFlowMonitor.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevFlowMonitor.Api.GitHub;

internal sealed class GitHubSynchronizationService(
    DevFlowDbContext dbContext,
    IGitHubApiClient gitHub,
    IOptions<GitHubSyncOptions> options,
    ILogger<GitHubSynchronizationService> logger) : IGitHubSynchronizationService
{
    private readonly GitHubSyncOptions _options = options.Value;

    public async Task<GitHubActionsResult<GitHubSyncResponse>> SynchronizeAsync(
        Guid accountId,
        string token,
        bool fullHistory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return GitHubActionsResult<GitHubSyncResponse>.Failed("Укажите GitHub token");

        var account = await dbContext.GitHubAccounts.SingleOrDefaultAsync(
            item => item.Id == accountId,
            ct);
        if (account is null)
            return GitHubActionsResult<GitHubSyncResponse>.Failed("GitHub-аккаунт не найден");

        var repositoriesResult = await LoadRepositoriesAsync(account.Owner, token, ct);
        if (!repositoriesResult.IsSuccess)
            return GitHubActionsResult<GitHubSyncResponse>.Failed(repositoriesResult.ErrorMessage!);

        var counters = new SyncCounters();
        List<string> warnings = [];

        foreach (var sourceRepository in repositoriesResult.Value!)
        {
            await SynchronizeRepositoryAsync(
                account,
                sourceRepository,
                token,
                fullHistory,
                counters,
                warnings,
                ct);
        }

        account.LastSynchronizedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return GitHubActionsResult<GitHubSyncResponse>.Success(new GitHubSyncResponse(
            account.Id,
            counters.Repositories,
            counters.Workflows.Count,
            counters.Runs,
            counters.Attempts,
            counters.Jobs,
            counters.Steps,
            account.LastSynchronizedAt.Value,
            warnings));
    }

    private async Task<GitHubActionsResult<IReadOnlyList<GitHubRepositoryResponse>>> LoadRepositoriesAsync(
        string owner,
        string token,
        CancellationToken ct)
    {
        List<GitHubRepositoryResponse> repositories = [];

        for (var page = 1; page <= _options.MaximumRepositoryPages; page++)
        {
            var result = await gitHub.GetAsync<IReadOnlyList<GitHubRepositoryResponse>>(
                $"user/repos?visibility=all&affiliation=owner,collaborator,organization_member&per_page=100&page={page}",
                token,
                "GitHub repositories synchronization",
                "Не удалось загрузить репозитории GitHub",
                ct);

            if (!result.IsSuccess)
                return GitHubActionsResult<IReadOnlyList<GitHubRepositoryResponse>>.Failed(result.ErrorMessage!);

            var pageRepositories = result.Value!;
            repositories.AddRange(pageRepositories.Where(repository =>
                repository is { Archived: false, Disabled: false, Id: > 0 }
                && string.Equals(repository.Owner.Login, owner, StringComparison.OrdinalIgnoreCase)));

            if (pageRepositories.Count < 100)
                break;
        }

        return GitHubActionsResult<IReadOnlyList<GitHubRepositoryResponse>>.Success(repositories);
    }

    private async Task SynchronizeRepositoryAsync(
        GitHubAccount account,
        GitHubRepositoryResponse source,
        string token,
        bool fullHistory,
        SyncCounters counters,
        List<string> warnings,
        CancellationToken ct)
    {
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(
            item => item.AccountId == account.Id && item.ExternalId == source.Id,
            ct);

        if (repository is null)
        {
            repository = new Repository
            {
                Id = Guid.NewGuid(),
                ExternalId = source.Id,
                AccountId = account.Id,
                Owner = source.Owner.Login,
                Name = source.Name,
                FullName = source.FullName,
                DefaultBranch = source.DefaultBranch ?? "main"
            };
            dbContext.Repositories.Add(repository);
        }

        repository.Owner = source.Owner.Login;
        repository.Name = source.Name;
        repository.FullName = source.FullName;
        repository.DefaultBranch = source.DefaultBranch ?? repository.DefaultBranch;
        repository.IsPrivate = source.IsPrivate;
        repository.IsArchived = source.Archived;
        counters.Repositories++;

        var runsResult = await LoadRunsAsync(source, token, fullHistory, warnings, ct);

        if (!runsResult.IsSuccess)
        {
            warnings.Add($"{source.FullName}: {runsResult.ErrorMessage}");
            logger.LogWarning(
                "Skipping workflow runs for {Repository}: {ErrorMessage}",
                source.FullName,
                runsResult.ErrorMessage);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var sourceRuns = runsResult.Value!;
        var workflowIds = sourceRuns.Select(run => run.WorkflowId).Distinct().ToArray();
        var workflows = await dbContext.Workflows
            .Where(workflow => workflow.RepositoryId == repository.Id
                               && workflowIds.Contains(workflow.ExternalId))
            .ToDictionaryAsync(workflow => workflow.ExternalId, ct);
        var events = await dbContext.Events
            .Where(triggerEvent => triggerEvent.RepositoryId == repository.Id)
            .ToDictionaryAsync(triggerEvent => triggerEvent.Name, StringComparer.OrdinalIgnoreCase, ct);
        var commits = await dbContext.Commits
            .Where(commit => commit.RepositoryId == repository.Id)
            .ToDictionaryAsync(commit => commit.Sha, StringComparer.OrdinalIgnoreCase, ct);
        var runIds = sourceRuns.Select(run => run.Id).ToArray();
        var runs = await dbContext.WorkflowRuns
            .Where(run => run.Workflow.RepositoryId == repository.Id
                          && runIds.Contains(run.ExternalId))
            .Include(run => run.Attempts)
                .ThenInclude(attempt => attempt.Jobs)
                .ThenInclude(job => job.Steps)
            .ToDictionaryAsync(run => run.ExternalId, ct);
        var actorIds = sourceRuns
            .Where(run => run.Actor is { Id: > 0 })
            .Select(run => run.Actor!.Id)
            .Distinct()
            .ToArray();
        var users = await dbContext.Users
            .Where(user => actorIds.Contains(user.ExternalId))
            .ToDictionaryAsync(user => user.ExternalId, ct);

        for (var index = 0; index < sourceRuns.Count; index++)
        {
            var sourceRun = sourceRuns[index];
            var workflow = GetOrCreateWorkflow(repository, sourceRun, workflows);
            counters.Workflows.Add(workflow.Id);
            var triggerEvent = GetOrCreateEvent(repository, sourceRun.Event, events);
            var commit = GetOrCreateCommit(repository, sourceRun, commits);
            var actor = GetOrCreateUser(sourceRun.Actor, users);
            var needsJobSynchronization = !runs.TryGetValue(sourceRun.Id, out var existingRun)
                                          || existingRun.UpdatedAt != (sourceRun.UpdatedAt
                                              ?? sourceRun.RunStartedAt
                                              ?? sourceRun.CreatedAt)
                                          || !string.Equals(
                                              existingRun.Status,
                                              sourceRun.Status,
                                              StringComparison.OrdinalIgnoreCase)
                                          || !string.Equals(
                                              existingRun.Conclusion,
                                              sourceRun.Conclusion,
                                              StringComparison.OrdinalIgnoreCase);
            var run = GetOrCreateRun(workflow, sourceRun, runs);

            UpdateRun(run, sourceRun, triggerEvent, commit, actor);
            counters.Runs++;

            if (index < _options.RunsWithJobDetailsPerRepository
                && (fullHistory || needsJobSynchronization))
                await SynchronizeAttemptsAsync(repository, run, sourceRun, token, counters, warnings, ct);
            else
                EnsureAttempts(run, sourceRun, counters);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<GitHubActionsResult<IReadOnlyList<GitHubWorkflowRun>>> LoadRunsAsync(
        GitHubRepositoryResponse repository,
        string token,
        bool fullHistory,
        List<string> warnings,
        CancellationToken ct)
    {
        List<GitHubWorkflowRun> runs = [];
        var totalCount = 0;

        var maximumPages = fullHistory ? _options.MaximumRunPagesPerRepository : 1;
        for (var page = 1; page <= maximumPages; page++)
        {
            var result = await gitHub.GetAsync<GitHubWorkflowRunsResponse>(
                $"repos/{Uri.EscapeDataString(repository.Owner.Login)}/{Uri.EscapeDataString(repository.Name)}/actions/runs?per_page={_options.RunsPerPage}&page={page}",
                token,
                "GitHub workflow runs synchronization",
                $"Не удалось загрузить запуски {repository.FullName}",
                ct);
            if (!result.IsSuccess)
                return GitHubActionsResult<IReadOnlyList<GitHubWorkflowRun>>.Failed(result.ErrorMessage!);

            totalCount = result.Value!.TotalCount;
            runs.AddRange(result.Value.WorkflowRuns);
            if (result.Value.WorkflowRuns.Count < _options.RunsPerPage)
                break;
        }

        if (fullHistory && runs.Count < totalCount)
            warnings.Add($"{repository.FullName}: загружено {runs.Count} из {totalCount} запусков; увеличьте лимит страниц синхронизации при необходимости");

        return GitHubActionsResult<IReadOnlyList<GitHubWorkflowRun>>.Success(runs);
    }

    private Workflow GetOrCreateWorkflow(
        Repository repository,
        GitHubWorkflowRun source,
        IDictionary<long, Workflow> workflows)
    {
        if (workflows.TryGetValue(source.WorkflowId, out var workflow))
            return workflow;

        workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ExternalId = source.WorkflowId,
            RepositoryId = repository.Id,
            Name = GetWorkflowName(source),
            Path = source.Path,
            State = "active"
        };
        workflows.Add(source.WorkflowId, workflow);
        dbContext.Workflows.Add(workflow);
        return workflow;
    }

    private TriggerEvent? GetOrCreateEvent(
        Repository repository,
        string? eventName,
        IDictionary<string, TriggerEvent> events)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return null;
        if (events.TryGetValue(eventName, out var triggerEvent))
            return triggerEvent;

        triggerEvent = new TriggerEvent
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Name = eventName
        };
        events.Add(eventName, triggerEvent);
        dbContext.Events.Add(triggerEvent);
        return triggerEvent;
    }

    private GitCommit? GetOrCreateCommit(
        Repository repository,
        GitHubWorkflowRun source,
        IDictionary<string, GitCommit> commits)
    {
        var sha = source.HeadCommit?.Sha ?? source.HeadSha;
        if (string.IsNullOrWhiteSpace(sha))
            return null;

        if (!commits.TryGetValue(sha, out var commit))
        {
            commit = new GitCommit
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Sha = sha
            };
            commits.Add(sha, commit);
            dbContext.Commits.Add(commit);
        }

        commit.Message = source.HeadCommit?.Message ?? commit.Message;
        commit.AuthorName = source.HeadCommit?.Author?.Name ?? commit.AuthorName;
        commit.AuthorEmail = source.HeadCommit?.Author?.Email ?? commit.AuthorEmail;
        commit.AuthoredAt = source.HeadCommit?.Timestamp ?? commit.AuthoredAt;
        return commit;
    }

    private GitHubUser? GetOrCreateUser(
        GitHubActorResponse? source,
        IDictionary<long, GitHubUser> users)
    {
        if (source is null || source.Id <= 0)
            return null;

        if (!users.TryGetValue(source.Id, out var user))
        {
            user = new GitHubUser
            {
                Id = Guid.NewGuid(),
                ExternalId = source.Id,
                Login = source.Login
            };
            users.Add(source.Id, user);
            dbContext.Users.Add(user);
        }

        user.Login = source.Login;
        user.AvatarUrl = source.AvatarUrl;
        user.HtmlUrl = source.HtmlUrl;
        return user;
    }

    private WorkflowRun GetOrCreateRun(
        Workflow workflow,
        GitHubWorkflowRun source,
        IDictionary<long, WorkflowRun> runs)
    {
        if (runs.TryGetValue(source.Id, out var run))
            return run;

        run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            ExternalId = source.Id,
            WorkflowId = workflow.Id,
            DisplayTitle = string.Empty,
            Branch = string.Empty,
            Status = string.Empty
        };
        runs.Add(source.Id, run);
        dbContext.WorkflowRuns.Add(run);
        return run;
    }

    private static void UpdateRun(
        WorkflowRun run,
        GitHubWorkflowRun source,
        TriggerEvent? triggerEvent,
        GitCommit? commit,
        GitHubUser? actor)
    {
        var createdAt = source.CreatedAt ?? source.RunStartedAt ?? DateTimeOffset.UtcNow;
        var startedAt = source.RunStartedAt ?? createdAt;
        var updatedAt = source.UpdatedAt ?? startedAt;

        run.RunNumber = source.RunNumber;
        run.DisplayTitle = FirstNotEmpty(source.DisplayTitle, source.Name) ?? $"Run {source.RunNumber}";
        run.Branch = FirstNotEmpty(source.HeadBranch) ?? "-";
        run.Status = (FirstNotEmpty(source.Status) ?? "unknown").ToLowerInvariant();
        run.Conclusion = source.Conclusion?.Trim().ToLowerInvariant();
        run.Event = triggerEvent;
        run.Commit = commit;
        run.Actor = actor;
        run.CreatedAt = createdAt;
        run.StartedAt = startedAt;
        run.UpdatedAt = updatedAt;
        run.CompletedAt = string.Equals(source.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? updatedAt
            : null;
        run.HtmlUrl = source.HtmlUrl;
    }

    private async Task SynchronizeAttemptsAsync(
        Repository repository,
        WorkflowRun run,
        GitHubWorkflowRun source,
        string token,
        SyncCounters counters,
        List<string> warnings,
        CancellationToken ct)
    {
        var attemptCount = Math.Max(source.RunAttempt, 1);
        for (var attemptNumber = 1; attemptNumber <= attemptCount; attemptNumber++)
        {
            var result = await gitHub.GetAsync<GitHubJobsResponse>(
                $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/actions/runs/{source.Id}/attempts/{attemptNumber}/jobs?per_page=100",
                token,
                "GitHub jobs synchronization",
                $"Не удалось загрузить jobs запуска {source.Id}",
                ct);

            var attempt = GetOrCreateAttempt(run, attemptNumber);

            counters.Attempts++;

            if (!result.IsSuccess)
            {
                UpdateAttemptFromRun(attempt, source, attemptNumber == attemptCount);
                warnings.Add($"{repository.FullName}, run {source.RunNumber}, attempt {attemptNumber}: {result.ErrorMessage}");
                continue;
            }

            UpdateAttempt(attempt, result.Value!.Jobs, source, attemptNumber == attemptCount);
            SynchronizeJobs(attempt, result.Value.Jobs, counters);
        }
    }

    private static void EnsureAttempts(
        WorkflowRun run,
        GitHubWorkflowRun source,
        SyncCounters counters)
    {
        var attemptCount = Math.Max(source.RunAttempt, 1);
        for (var number = 1; number <= attemptCount; number++)
        {
            var attempt = GetOrCreateAttempt(run, number);

            UpdateAttemptFromRun(attempt, source, isCurrent: number == attemptCount);
            counters.Attempts++;
        }
    }

    private static void UpdateAttempt(
        Attempt attempt,
        IReadOnlyList<GitHubJobResponse> jobs,
        GitHubWorkflowRun source,
        bool isCurrent)
    {
        attempt.StartedAt = jobs.Where(job => job.StartedAt.HasValue)
            .Select(job => job.StartedAt!.Value)
            .DefaultIfEmpty(source.RunStartedAt ?? source.CreatedAt ?? DateTimeOffset.UtcNow)
            .Min();
        attempt.CompletedAt = jobs.Any(job => job.CompletedAt.HasValue)
            ? jobs.Where(job => job.CompletedAt.HasValue).Max(job => job.CompletedAt)
            : isCurrent && string.Equals(source.Status, "completed", StringComparison.OrdinalIgnoreCase)
                ? source.UpdatedAt
                : null;
        attempt.Status = jobs.Count == 0
            ? source.Status ?? "unknown"
            : jobs.All(job => string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase))
                ? "completed"
                : "in_progress";
        attempt.Conclusion = jobs.Count == 0
            ? isCurrent ? source.Conclusion : null
            : GetAggregateConclusion(jobs.Select(job => job.Conclusion));
    }

    private static void UpdateAttemptFromRun(Attempt attempt, GitHubWorkflowRun source, bool isCurrent)
    {
        attempt.Status = isCurrent ? source.Status ?? "unknown" : attempt.Status;
        attempt.Conclusion = isCurrent ? source.Conclusion : attempt.Conclusion;
        attempt.StartedAt = source.RunStartedAt ?? source.CreatedAt ?? attempt.StartedAt;
        attempt.CompletedAt = isCurrent
                              && string.Equals(source.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? source.UpdatedAt
            : attempt.CompletedAt;
    }

    private static void SynchronizeJobs(
        Attempt attempt,
        IReadOnlyList<GitHubJobResponse> sourceJobs,
        SyncCounters counters)
    {
        var jobsByExternalId = attempt.Jobs.ToDictionary(job => job.ExternalId);

        foreach (var sourceJob in sourceJobs)
        {
            if (!jobsByExternalId.TryGetValue(sourceJob.Id, out var job))
            {
                job = new Job
                {
                    Id = Guid.NewGuid(),
                    ExternalId = sourceJob.Id,
                    AttemptId = attempt.Id,
                    Name = sourceJob.Name,
                    Status = sourceJob.Status ?? "unknown"
                };
                attempt.Jobs.Add(job);
                jobsByExternalId.Add(job.ExternalId, job);
            }

            job.Name = sourceJob.Name;
            job.Status = sourceJob.Status ?? "unknown";
            job.Conclusion = sourceJob.Conclusion;
            job.RunnerName = sourceJob.RunnerName;
            job.StartedAt = sourceJob.StartedAt;
            job.CompletedAt = sourceJob.CompletedAt;
            counters.Jobs++;

            SynchronizeSteps(job, sourceJob.Steps ?? [], counters);
        }
    }

    private static void SynchronizeSteps(
        Job job,
        IReadOnlyList<GitHubStepResponse> sourceSteps,
        SyncCounters counters)
    {
        var stepsByNumber = job.Steps.ToDictionary(step => step.Number);

        foreach (var sourceStep in sourceSteps)
        {
            if (!stepsByNumber.TryGetValue(sourceStep.Number, out var step))
            {
                step = new Step
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Number = sourceStep.Number,
                    Name = sourceStep.Name,
                    Status = sourceStep.Status ?? "unknown"
                };
                job.Steps.Add(step);
                stepsByNumber.Add(step.Number, step);
            }

            step.Name = sourceStep.Name;
            step.Status = sourceStep.Status ?? "unknown";
            step.Conclusion = sourceStep.Conclusion;
            step.StartedAt = sourceStep.StartedAt;
            step.CompletedAt = sourceStep.CompletedAt;
            counters.Steps++;
        }
    }

    private static Attempt GetOrCreateAttempt(WorkflowRun run, int attemptNumber)
    {
        var attempt = run.Attempts.SingleOrDefault(item => item.Number == attemptNumber);
        if (attempt is not null)
            return attempt;

        attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Number = attemptNumber,
            Status = "unknown",
            StartedAt = run.StartedAt
        };
        run.Attempts.Add(attempt);
        return attempt;
    }

    private static string? GetAggregateConclusion(IEnumerable<string?> conclusions)
    {
        var values = conclusions.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (values.Length == 0)
            return null;
        if (values.Any(value => string.Equals(value, "failure", StringComparison.OrdinalIgnoreCase)))
            return "failure";
        if (values.All(value => string.Equals(value, "success", StringComparison.OrdinalIgnoreCase)))
            return "success";
        return values[0];
    }

    private static string GetWorkflowName(GitHubWorkflowRun run) =>
        FirstNotEmpty(run.Name, run.Path) ?? $"workflow-{run.WorkflowId}";

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed class SyncCounters
    {
        public int Repositories { get; set; }
        public HashSet<Guid> Workflows { get; } = [];
        public int Runs { get; set; }
        public int Attempts { get; set; }
        public int Jobs { get; set; }
        public int Steps { get; set; }
    }
}
