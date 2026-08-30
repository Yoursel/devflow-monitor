using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;

namespace DevFlowMonitor.Wpf.ViewModel;

public class DashboardViewModel : INotifyPropertyChanged, IActivatableViewModel
{
    private readonly IDevFlowApiClient _apiClient;

    public DashboardViewModel(IDevFlowApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());

        StatusCards.Add(TotalRunsCard);
        StatusCards.Add(SuccessfulRunsCard);
        StatusCards.Add(FailedRunsCard);
    }

    public ObservableCollection<PipelineViewModel> PipelineRuns { get; } = [];
    public ObservableCollection<StatusCardViewModel> StatusCards { get; } = [];
    public ICommand RefreshCommand { get; }

    public Task ActivateAsync(CancellationToken ct = default) =>
        LoadAsync(ct);

    private StatusCardViewModel TotalRunsCard { get; } =
        new() { Title = "ВСЕГО ЗАПУСКОВ", Type = StatusCardType.Total };

    private StatusCardViewModel SuccessfulRunsCard { get; } =
        new() { Title = "УСПЕШНЫХ", Type = StatusCardType.Success };

    private StatusCardViewModel FailedRunsCard { get; } =
        new() { Title = "УПАВШИХ", Type = StatusCardType.Failed };

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        StatusMessage = "Загрузка dashboard...";

        try
        {
            var result = await _apiClient.GetDashboardAsync(ct);

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage!;
                return;
            }

            var summary = result.Summary!;

            TotalRunsCard.Value = summary.TotalRuns;
            SuccessfulRunsCard.Value = summary.SuccessfulRuns;
            FailedRunsCard.Value = summary.FailedRuns;

            PipelineRuns.Clear();

            foreach (var pipeline in summary.RecentPipelines)
                PipelineRuns.Add(PipelineViewModelMapper.Map(pipeline));

            StatusMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
