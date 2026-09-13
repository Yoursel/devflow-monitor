namespace DevFlowMonitor.Api.GitHub;

internal static class GitHubActionsEndpointResultExtensions
{
    public static IResult ToHttpResult<T>(this GitHubActionsResult<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Text(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest);
}
