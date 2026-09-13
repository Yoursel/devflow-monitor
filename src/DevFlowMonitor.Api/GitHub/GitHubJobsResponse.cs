using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubJobsResponse(
    [property: JsonPropertyName("total_count")]
    int TotalCount,
    [property: JsonPropertyName("jobs")]
    IReadOnlyList<GitHubJobResponse> Jobs);

internal sealed record GitHubJobResponse(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("status")]
    string? Status,
    [property: JsonPropertyName("conclusion")]
    string? Conclusion,
    [property: JsonPropertyName("runner_name")]
    string? RunnerName,
    [property: JsonPropertyName("started_at")]
    DateTimeOffset? StartedAt,
    [property: JsonPropertyName("completed_at")]
    DateTimeOffset? CompletedAt,
    [property: JsonPropertyName("steps")]
    IReadOnlyList<GitHubStepResponse>? Steps);

internal sealed record GitHubStepResponse(
    [property: JsonPropertyName("number")]
    int Number,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("status")]
    string? Status,
    [property: JsonPropertyName("conclusion")]
    string? Conclusion,
    [property: JsonPropertyName("started_at")]
    DateTimeOffset? StartedAt,
    [property: JsonPropertyName("completed_at")]
    DateTimeOffset? CompletedAt);
