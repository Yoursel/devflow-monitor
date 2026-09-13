using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubRepositoryResponse(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("full_name")]
    string FullName,
    [property: JsonPropertyName("owner")]
    GitHubRepositoryOwner Owner,
    [property: JsonPropertyName("archived")]
    bool Archived,
    [property: JsonPropertyName("disabled")]
    bool Disabled,
    [property: JsonPropertyName("private")]
    bool IsPrivate,
    [property: JsonPropertyName("default_branch")]
    string? DefaultBranch);
