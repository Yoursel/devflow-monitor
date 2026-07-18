using DevFlowMonitor.Api;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IGitHubActionsClient, GitHubActionsClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var pipelines = PipelineDemoData.Pipelines;

static IResult ToApiResult<T>(GitHubActionsResult<T> result) =>
    result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Text(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest);

app.MapGet("/api/health", () => new HealthResponse(
    Status: ApiHealthStatus.Healthy,
    Version: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    Timestamp: DateTimeOffset.UtcNow));

app.MapGet("/api/dashboard", () => new DashboardSummaryResponse(
    TotalRuns: pipelines.Sum(pipeline => pipeline.SuccessfulRuns + pipeline.FailedRuns),
    SuccessfulRuns: pipelines.Sum(pipeline => pipeline.SuccessfulRuns),
    FailedRuns: pipelines.Sum(pipeline => pipeline.FailedRuns),
    RecentPipelines: pipelines
        .OrderByDescending(pipeline => pipeline.StartedAt)
        .Take(4)
        .ToArray()));

app.MapGet("/api/pipelines", (int page = 1, int pageSize = 5) =>
{
    if (page < 1)
        return Results.BadRequest("Page must be greater than or equal to 1.");

    if (pageSize is < 1 or > 50)
        return Results.BadRequest("PageSize must be between 1 and 50.");

    var items = pipelines
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToArray();

    return Results.Ok(new PagedResponse<PipelineSummaryResponse>(
        Items: items,
        Page: page,
        PageSize: pageSize,
        TotalItems: pipelines.Count));
});

app.MapPost(
    "/api/github/check-connection",
    async (
        GitHubConnectionRequest request,
        IGitHubActionsClient gitHub,
        CancellationToken ct) =>
        ToApiResult(await gitHub.CheckConnectionAsync(request, ct)));

app.MapPost(
    "/api/github/dashboard",
    async (
        GitHubConnectionRequest request,
        IGitHubActionsClient gitHub,
        CancellationToken ct) =>
        ToApiResult(await gitHub.GetDashboardAsync(request, ct)));

app.MapPost(
    "/api/github/pipelines",
    async (
        GitHubPipelinesRequest request,
        IGitHubActionsClient gitHub,
        CancellationToken ct) =>
        ToApiResult(await gitHub.GetPipelinesAsync(request, ct)));

app.Run();
