using DevFlowMonitor.Wpf.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace DevFlowMonitor.Wpf.ViewModel;

public class DashboardViewModel : INotifyPropertyChanged
{

    public ObservableCollection<PipelinesViewModel> PipelineRuns { get; } =
    [
        new()
        {
            Status = PipelineStatus.Success, PipelineName = "backend-ci", Branch = "main", TimeAgo = "5 ÏËÌ Ì‡Á‡‰"
        },

        new()
        {
            Status = PipelineStatus.Failed, PipelineName = "frontend-deploy", Branch = "feature/auth",
            TimeAgo = "12 ÏËÌ Ì‡Á‡‰"
        },

        new()
        {
            Status = PipelineStatus.Running, PipelineName = "data-pipeline", Branch = "develop",
            TimeAgo = "28 ÏËÌ Ì‡Á‡‰"
        },

        new() { Status = PipelineStatus.Success, PipelineName = "auth-service", Branch = "main", TimeAgo = "1 ˜ Ì‡Á‡‰" }
    ];

    public ObservableCollection<StatusCardViewModel> StatusCards { get; } =
    [
        new() { Title = "¬—≈√Œ «¿œ”— Œ¬", Type = StatusCardType.Total, Value = 26 },
        new() { Title = "”—œ≈ÿÕ€’", Type = StatusCardType.Success, Value = 24 },
        new() { Title = "”œ¿¬ÿ»’", Type = StatusCardType.Failed, Value = 2 }
    ];

    #region OnPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}