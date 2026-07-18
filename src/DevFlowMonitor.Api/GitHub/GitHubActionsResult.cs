namespace DevFlowMonitor.Api.GitHub;

internal sealed record GitHubActionsResult<T>(T? Value, string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage is null;

    public static GitHubActionsResult<T> Success(T value) =>
        new(value, null);

    public static GitHubActionsResult<T> Failed(string message) =>
        new(default, message);
}
