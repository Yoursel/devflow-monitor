using DevFlowMonitor.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/health", () => new HealthResponse(
    Status: ApiHealthStatus.Healthy,
    Version: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    Timestamp: DateTimeOffset.UtcNow));

app.Run();
