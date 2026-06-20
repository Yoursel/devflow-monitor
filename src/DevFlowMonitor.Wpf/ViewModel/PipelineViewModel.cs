using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PipelineViewModel
{
    public PipelineStatus Status { get; init; }
    public string PipelineName { get; init; } = "";
    public string Branch { get; init; } = "";
    public string TimeAgo { get; init; } = "";
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }
}