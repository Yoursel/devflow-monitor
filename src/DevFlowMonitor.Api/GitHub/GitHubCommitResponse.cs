using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubCommitResponse(
    [property: JsonPropertyName("id")]
    string Sha,
    [property: JsonPropertyName("message")]
    string? Message,
    [property: JsonPropertyName("timestamp")]
    DateTimeOffset? Timestamp,
    [property: JsonPropertyName("author")]
    GitHubCommitAuthorResponse? Author);

internal sealed record GitHubCommitAuthorResponse(
    [property: JsonPropertyName("name")]
    string? Name,
    [property: JsonPropertyName("email")]
    string? Email);
