using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubActorResponse(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("login")]
    string Login,
    [property: JsonPropertyName("avatar_url")]
    string? AvatarUrl,
    [property: JsonPropertyName("html_url")]
    string? HtmlUrl);
