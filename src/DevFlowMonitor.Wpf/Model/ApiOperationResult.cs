namespace DevFlowMonitor.Wpf.Model;

public sealed record ApiOperationResult<T>(T? Value, string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage is null;

    public static ApiOperationResult<T> Success(T value) => new(value, null);
    public static ApiOperationResult<T> Failed(string message) => new(default, message);
}
