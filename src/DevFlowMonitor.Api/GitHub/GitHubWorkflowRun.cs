using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubWorkflowRun(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("workflow_id")]
    long WorkflowId,
    [property: JsonPropertyName("run_number")]
    long RunNumber,
    [property: JsonPropertyName("name")]
    string? Name,
    [property: JsonPropertyName("display_title")]
    string? DisplayTitle,
    [property: JsonPropertyName("head_branch")]
    string? HeadBranch,
    [property: JsonPropertyName("status")]
    string? Status,
    [property: JsonPropertyName("conclusion")]
    string? Conclusion,
    [property: JsonPropertyName("run_started_at")]
    DateTimeOffset? RunStartedAt,
    [property: JsonPropertyName("created_at")]
    DateTimeOffset? CreatedAt);
