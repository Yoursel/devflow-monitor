using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubRepositoryOwner(
    [property: JsonPropertyName("login")]
    string Login);
