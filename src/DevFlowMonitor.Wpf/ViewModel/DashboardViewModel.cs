using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Contracts;
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
        ApplyPeriodCommand = new AsyncRelayCommand(() => LoadAsync());
        ResetPeriodCommand = new AsyncRelayCommand(ResetPeriodAsync);
        ToggleAllTimeCommand = new AsyncRelayCommand(ToggleAllTimeAsync);

        StatusCards.Add(TotalRunsCard);
        StatusCards.Add(SuccessfulRunsCard);
        StatusCards.Add(FailedRunsCard);
    }

    public ObservableCollection<PipelineViewModel> PipelineRuns { get; } = [];
    public ObservableCollection<StatusCardViewModel> StatusCards { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand ApplyPeriodCommand { get; }
    public ICommand ResetPeriodCommand { get; }
    public ICommand ToggleAllTimeCommand { get; }

    private DateTime? _periodStartDate = DateTime.Today.AddDays(-29);
    public DateTime? PeriodStartDate
    {
        get => _periodStartDate;
        set
        {
            if (!SetField(ref _periodStartDate, value))
                return;

            IsAllTime = false;
            NotifyPeriodLabelsChanged();
        }
    }

    private DateTime? _periodEndDate = DateTime.Today;
    public DateTime? PeriodEndDate
    {
        get => _periodEndDate;
        set
        {
            if (!SetField(ref _periodEndDate, value))
                return;

            IsAllTime = false;
            NotifyPeriodLabelsChanged();
        }
    }

    private bool _isAllTime;
    public bool IsAllTime
    {
        get => _isAllTime;
        private set
        {
            if (!SetField(ref _isAllTime, value))
                return;

            OnPropertyChanged(nameof(ArePeriodDatesEnabled));
            OnPropertyChanged(nameof(AllTimeButtonText));
            NotifyPeriodLabelsChanged();
        }
    }

    public bool ArePeriodDatesEnabled => !IsAllTime;
    public string AllTimeButtonText => IsAllTime ? "Выбрать период" : "За всё время";
    public string StatisticsTitle => IsAllTime ? "Статистика за всё время" : $"Статистика: {PeriodLabel}";
    public string MetricsTitle => IsAllTime ? "Метрики за всё время" : $"Метрики: {PeriodLabel}";
    public string PageSubtitle => IsAllTime
        ? "Сводка состояния CI/CD и последние запуски за всё время"
        : $"Сводка состояния CI/CD и последние запуски за период {PeriodLabel}";

    private string PeriodLabel => PeriodStartDate.HasValue && PeriodEndDate.HasValue
        ? $"{PeriodStartDate:dd.MM.yyyy} — {PeriodEndDate:dd.MM.yyyy}"
        : "период не выбран";

    private MetricsSummaryResponse? _metrics;
    public MetricsSummaryResponse? Metrics
    {
        get => _metrics;
        private set
        {
            if (_metrics == value)
                return;
            _metrics = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TopFailingWorkflowName));
            OnPropertyChanged(nameof(TopFailingWorkflowSummary));
            OnPropertyChanged(nameof(TopFailingJobName));
            OnPropertyChanged(nameof(TopFailingJobSummary));
        }
    }

    public string TopFailingWorkflowName => TopFailingWorkflow?.Name ?? "Сбоев нет";
    public string TopFailingWorkflowSummary => FormatFailureSummary(TopFailingWorkflow);
    public string TopFailingJobName => TopFailingJob?.Name ?? "Сбоев нет";
    public string TopFailingJobSummary => FormatFailureSummary(TopFailingJob);

    private FailureFrequencyResponse? TopFailingWorkflow =>
        Metrics?.WorkflowFailures.FirstOrDefault(item => item.FailedExecutions > 0);

    private FailureFrequencyResponse? TopFailingJob =>
        Metrics?.JobFailures.FirstOrDefault(item => item.FailedExecutions > 0);

    private static string FormatFailureSummary(FailureFrequencyResponse? item) => item is null
        ? string.Empty
        : $"Сбоев: {item.FailedExecutions} из {item.TotalExecutions} ({item.FailureRate:F1}%)";

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

    private string _metricsStatusMessage = string.Empty;
    public string MetricsStatusMessage
    {
        get => _metricsStatusMessage;
        private set
        {
            if (_metricsStatusMessage == value)
                return;

            _metricsStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!TryGetPeriod(out var periodStart, out var periodEnd, out var errorMessage))
        {
            StatusMessage = errorMessage;
            return;
        }

        IsLoading = true;
        StatusMessage = "Загрузка dashboard...";

        try
        {
            var dashboardTask = _apiClient.GetDashboardAsync(periodStart, periodEnd, ct);
            var metricsTask = _apiClient.GetMetricsAsync(periodStart, periodEnd, ct);
            await Task.WhenAll(dashboardTask, metricsTask);
            var result = await dashboardTask;
            var metricsResult = await metricsTask;

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

            Metrics = metricsResult.IsSuccess ? metricsResult.Value : null;
            MetricsStatusMessage = metricsResult.IsSuccess
                ? string.Empty
                : metricsResult.ErrorMessage ?? "Не удалось загрузить метрики";

            StatusMessage = "Данные обновлены";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetPeriodAsync()
    {
        IsAllTime = false;
        PeriodStartDate = DateTime.Today.AddDays(-29);
        PeriodEndDate = DateTime.Today;
        await LoadAsync();
    }

    private async Task ToggleAllTimeAsync()
    {
        IsAllTime = !IsAllTime;
        if (IsAllTime)
        {
            await LoadAsync();
            return;
        }

        StatusMessage = "Выберите даты и нажмите «Применить»";
    }

    private bool TryGetPeriod(
        out DateTimeOffset periodStart,
        out DateTimeOffset periodEnd,
        out string errorMessage)
    {
        periodStart = default;
        periodEnd = default;
        if (IsAllTime)
        {
            periodStart = DateTimeOffset.UnixEpoch;
            periodEnd = ToUtcBoundary(DateTime.Today.AddDays(1));
            errorMessage = string.Empty;
            return true;
        }

        if (!PeriodStartDate.HasValue || !PeriodEndDate.HasValue)
        {
            errorMessage = "Укажите начало и окончание периода";
            return false;
        }

        if (PeriodStartDate.Value.Date > PeriodEndDate.Value.Date)
        {
            errorMessage = "Начало периода должно быть раньше окончания";
            return false;
        }

        periodStart = ToUtcBoundary(PeriodStartDate.Value.Date);
        periodEnd = ToUtcBoundary(PeriodEndDate.Value.Date.AddDays(1));
        errorMessage = string.Empty;
        return true;
    }

    private static DateTimeOffset ToUtcBoundary(DateTime localDate)
    {
        var unspecified = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private void NotifyPeriodLabelsChanged()
    {
        OnPropertyChanged(nameof(StatisticsTitle));
        OnPropertyChanged(nameof(MetricsTitle));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
