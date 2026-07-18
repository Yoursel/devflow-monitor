using System.Net;
using System.Text;
using DevFlowMonitor.Api.GitHub;
using DevFlowMonitor.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevFlowMonitor.Tests;

public class GitHubActionsClientTests
{
    [Fact]
    public void AggregateRuns_GroupsSameWorkflowAndCountsOutcomes()
    {
        var repository = new GitHubRepository("Yoursel", "HhSearchByFiltersBot", "Yoursel/HhSearchByFiltersBot");
        var now = DateTimeOffset.UtcNow;
        GitHubWorkflowRun[] runs =
        [
            Run(4, 42, "CI", "success", now),
            Run(3, 42, ".github/workflows/github-actions-demo.yml", "failure", now.AddMinutes(-1)),
            Run(2, 42, ".github/workflows/github-actions-demo.yml", "failure", now.AddMinutes(-2)),
            Run(1, 42, ".github/workflows/github-actions-demo.yml", "failure", now.AddMinutes(-3))
        ];

        var pipeline = Assert.Single(GitHubActionsClient.AggregateRuns(repository, runs));

        Assert.Equal("Yoursel/HhSearchByFiltersBot / CI", pipeline.PipelineName);
        Assert.Equal(PipelineStatus.Success, pipeline.Status);
        Assert.Equal(1, pipeline.SuccessfulRuns);
        Assert.Equal(3, pipeline.FailedRuns);
        Assert.Equal(now, pipeline.StartedAt);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("skipped")]
    [InlineData("neutral")]
    public void AggregateRuns_DoesNotCountNonFailureConclusionAsFailed(string conclusion)
    {
        var repository = new GitHubRepository("Yoursel", "Bot", "Yoursel/Bot");
        GitHubWorkflowRun[] runs =
        [
            Run(1, 42, "CI", conclusion, DateTimeOffset.UtcNow)
        ];

        var pipeline = Assert.Single(GitHubActionsClient.AggregateRuns(repository, runs));

        Assert.Equal(PipelineStatus.Cancelled, pipeline.Status);
        Assert.Equal(0, pipeline.SuccessfulRuns);
        Assert.Equal(0, pipeline.FailedRuns);
    }

    [Fact]
    public async Task GetPipelinesAsync_SkipsFailedRepositoryAndReturnsOtherResults()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/user/repos" => JsonResponse("""
                [
                  { "name": "broken", "full_name": "Yoursel/broken", "archived": false, "disabled": false, "owner": { "login": "Yoursel" } },
                  { "name": "working", "full_name": "Yoursel/working", "archived": false, "disabled": false, "owner": { "login": "Yoursel" } }
                ]
                """),
            "/repos/Yoursel/broken/actions/runs" => new HttpResponseMessage(HttpStatusCode.Forbidden),
            "/repos/Yoursel/working/actions/runs" => JsonResponse("""
                {
                  "total_count": 1,
                  "workflow_runs": [
                    {
                      "id": 10,
                      "workflow_id": 42,
                      "name": "CI",
                      "display_title": "commit",
                      "head_branch": "main",
                      "status": "completed",
                      "conclusion": "success",
                      "run_started_at": "2026-07-18T10:00:00Z",
                      "created_at": "2026-07-18T10:00:00Z"
                    }
                  ]
                }
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = new GitHubActionsClient(httpClient, NullLogger<GitHubActionsClient>.Instance);

        var result = await client.GetPipelinesAsync(
            new GitHubPipelinesRequest("Yoursel", "token", 1, 5));

        Assert.True(result.IsSuccess);
        var pipeline = Assert.Single(result.Value!.Items);
        Assert.Equal("Yoursel/working / CI", pipeline.PipelineName);
    }

    private static GitHubWorkflowRun Run(
        long id,
        long workflowId,
        string name,
        string conclusion,
        DateTimeOffset startedAt) =>
        new(
            id,
            workflowId,
            name,
            "commit title",
            "main",
            "completed",
            conclusion,
            startedAt,
            startedAt);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
