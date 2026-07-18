using System.Text.Json.Serialization;

namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubWorkflowRunsResponse(
    [property: JsonPropertyName("total_count")]
    int TotalCount,
    [property: JsonPropertyName("workflow_runs")]
    IReadOnlyList<GitHubWorkflowRun> WorkflowRuns);
