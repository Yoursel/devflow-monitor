using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using System.Windows.Input;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PipelineViewModel
{
    public PipelineViewModel(
        PipelineStatus status,
        string pipelineName,
        string branch,
        string timeAgo,
        int successfulRuns,
        int failedRuns,
        IReadOnlyList<PipelineRunResponse> runs,
        Action<PipelineViewModel>? openHistory)
    {
        Status = status;
        PipelineName = pipelineName;
        Branch = branch;
        TimeAgo = timeAgo;
        SuccessfulRuns = successfulRuns;
        FailedRuns = failedRuns;
        Runs = runs;
        OpenHistoryCommand = new RelayCommand(
            () => openHistory?.Invoke(this),
            () => openHistory is not null && Runs.Count > 0);
    }

    public PipelineStatus Status { get; }
    public string PipelineName { get; }
    public string Branch { get; }
    public string TimeAgo { get; }
    public int SuccessfulRuns { get; }
    public int FailedRuns { get; }
    public IReadOnlyList<PipelineRunResponse> Runs { get; }
    public ICommand OpenHistoryCommand { get; }
}
