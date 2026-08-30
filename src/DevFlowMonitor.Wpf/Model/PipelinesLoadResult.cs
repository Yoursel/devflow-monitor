using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.Model;

public record PipelinesLoadResult(
    IReadOnlyList<PipelineSummaryResponse> Items,
    int TotalItems,
    string? ErrorMessage = null)
{
    public bool IsSuccess => ErrorMessage is null;

    public static PipelinesLoadResult Failed(string message) =>
        new([], 0, message);
}
