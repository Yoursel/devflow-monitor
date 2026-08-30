using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.ViewModel;

public sealed record PipelineStatusFilterOption(string Label, PipelineStatus? Value);
