using DevFlowMonitor.Api.Analytics;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Api.Metrics;
using DevFlowMonitor.Api.Pipelines;
using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Api.Accounts;

internal static class GitHubAccountEndpoints
{
    public static IEndpointRouteBuilder MapGitHubAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/api/github/accounts");

        accounts.MapGet("/", async (
            IGitHubAccountService service,
            CancellationToken ct) => Results.Ok(await service.GetAllAsync(ct)));

        accounts.MapPost("/", async (
            CreateGitHubAccountRequest request,
            IGitHubAccountService service,
            CancellationToken ct) => (await service.AddAsync(request, ct)).ToHttpResult());

        accounts.MapDelete("/{accountId:guid}", async (
            Guid accountId,
            IGitHubAccountService service,
            CancellationToken ct) =>
            await service.DeleteAsync(accountId, ct) ? Results.NoContent() : Results.NotFound());

        accounts.MapPost("/{accountId:guid}/sync", async (
            Guid accountId,
            GitHubAccountSyncRequest request,
            IGitHubSynchronizationService service,
            CancellationToken ct) => (await service.SynchronizeAsync(
                accountId,
                request.Token,
                request.FullHistory,
                ct)).ToHttpResult());

        accounts.MapGet("/{accountId:guid}/repositories", async (
            Guid accountId,
            IGitHubAccountService service,
            CancellationToken ct) => Results.Ok(await service.GetRepositoriesAsync(accountId, ct)));

        accounts.MapGet("/{accountId:guid}/dashboard", async (
            Guid accountId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            IStoredPipelineService service,
            CancellationToken ct) =>
        {
            var (periodStart, periodEnd) = ResolvePeriod(from, to);
            if (periodStart >= periodEnd)
                return Results.BadRequest("Параметр from должен быть меньше to");

            var result = await service.GetDashboardAsync(accountId, periodStart, periodEnd, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        accounts.MapGet("/{accountId:guid}/pipelines", async (
            Guid accountId,
            int page,
            int pageSize,
            string? search,
            string? branch,
            PipelineStatus? status,
            IStoredPipelineService service,
            CancellationToken ct) =>
        {
            if (page < 1)
                return Results.BadRequest("Page must be greater than or equal to 1.");
            if (pageSize is < 1 or > 50)
                return Results.BadRequest("PageSize must be between 1 and 50.");

            return Results.Ok(await service.GetPipelinesAsync(
                accountId,
                page,
                pageSize,
                search,
                branch,
                status,
                ct));
        });

        accounts.MapGet("/{accountId:guid}/metrics", async (
            Guid accountId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? repositoryId,
            Guid? workflowId,
            IMetricsService service,
            CancellationToken ct) =>
        {
            var (periodStart, periodEnd) = ResolvePeriod(from, to);
            if (periodStart >= periodEnd)
                return Results.BadRequest("Параметр from должен быть меньше to");

            var result = await service.GetAsync(
                accountId,
                periodStart,
                periodEnd,
                repositoryId,
                workflowId,
                ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        accounts.MapGet("/{accountId:guid}/analytics", async (
            Guid accountId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? repositoryId,
            Guid? workflowId,
            PipelineStatus? status,
            bool? allTime,
            IAnalyticsService service,
            CancellationToken ct) =>
        {
            var (periodStart, periodEnd) = ResolvePeriod(from, to);
            if (allTime != true && periodStart >= periodEnd)
                return Results.Text("Параметр from должен быть меньше to", statusCode: StatusCodes.Status400BadRequest);

            var result = await service.GetAsync(
                accountId,
                periodStart,
                periodEnd,
                repositoryId,
                workflowId,
                status,
                allTime == true,
                ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        accounts.MapGet("/{accountId:guid}/runs/{runId:guid}", async (
            Guid accountId,
            Guid runId,
            IStoredPipelineService service,
            CancellationToken ct) =>
        {
            var result = await service.GetRunDetailsAsync(accountId, runId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        endpoints.MapGet("/api/repositories/{repositoryId:guid}/workflows", async (
            Guid repositoryId,
            IGitHubAccountService service,
            CancellationToken ct) => Results.Ok(await service.GetWorkflowsAsync(repositoryId, ct)));

        return endpoints;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        return (from ?? end.AddDays(-30), end);
    }
}
