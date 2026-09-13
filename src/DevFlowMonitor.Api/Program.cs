using DevFlowMonitor.Api;
using DevFlowMonitor.Api.Accounts;
using DevFlowMonitor.Api.Analytics;
using DevFlowMonitor.Api.Data;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Api.Metrics;
using DevFlowMonitor.Api.Pipelines;
using DevFlowMonitor.Api.Security;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ILocalApiKeyProvider, LocalApiKeyProvider>();
builder.Services
    .AddAuthentication(LocalApiAuthentication.Scheme)
    .AddScheme<AuthenticationSchemeOptions, LocalApiAuthenticationHandler>(
        LocalApiAuthentication.Scheme,
        _ => { });
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
    new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.Configure<GitHubSyncOptions>(
    builder.Configuration.GetSection(GitHubSyncOptions.SectionName));
builder.Services.AddDbContext<DevFlowDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DevFlow")));
builder.Services.AddHttpClient<IGitHubApiClient, GitHubApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IGitHubActionsClient, GitHubActionsClient>();
builder.Services.AddScoped<IGitHubAccountService, GitHubAccountService>();
builder.Services.AddScoped<IGitHubSynchronizationService, GitHubSynchronizationService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IStoredPipelineService, StoredPipelineService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

var pipelines = PipelineDemoData.Pipelines;

app.MapGet("/api/health", () => new HealthResponse(
    Status: ApiHealthStatus.Healthy,
    Version: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    Timestamp: DateTimeOffset.UtcNow)).AllowAnonymous();

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
        (await gitHub.CheckConnectionAsync(request, ct)).ToHttpResult());

app.MapPost(
    "/api/github/dashboard",
    async (
        GitHubConnectionRequest request,
        IGitHubActionsClient gitHub,
        CancellationToken ct) =>
        (await gitHub.GetDashboardAsync(request, ct)).ToHttpResult());

app.MapPost(
    "/api/github/pipelines",
    async (
        GitHubPipelinesRequest request,
        IGitHubActionsClient gitHub,
        CancellationToken ct) =>
        (await gitHub.GetPipelinesAsync(request, ct)).ToHttpResult());

app.MapGitHubAccountEndpoints();

app.Run();
