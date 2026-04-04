using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PipelineViewModel
{
    public PipelineStatus Status { get; init; }
    public string PipelineName { get; init; } = "";
    public string Branch { get; init; } = "";
    public string TimeAgo { get; init; } = "";
}