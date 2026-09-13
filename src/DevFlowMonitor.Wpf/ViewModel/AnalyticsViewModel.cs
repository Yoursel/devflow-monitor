using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;

namespace DevFlowMonitor.Wpf.ViewModel;

public sealed class AnalyticsViewModel : INotifyPropertyChanged, IActivatableViewModel
{
    private readonly IDevFlowApiClient _apiClient;
    private CancellationTokenSource? _workflowLoadCancellation;
    private bool _isUpdatingFilters;
    private bool _isUpdatingPeriod;

    public AnalyticsViewModel(IDevFlowApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(() => LoadAnalyticsAsync());
        ApplyFiltersCommand = new AsyncRelayCommand(() => LoadAnalyticsAsync());
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync);
        ToggleAllTimeCommand = new AsyncRelayCommand(ToggleAllTimeAsync);
    }

    public ObservableCollection<AnalyticsScopeOption> Repositories { get; } = [];
    public ObservableCollection<AnalyticsScopeOption> Workflows { get; } = [];
    public IReadOnlyList<PipelineStatusFilterOption> AvailableStatuses { get; } =
    [
        new("Все статусы", null),
        new("Успешные", PipelineStatus.Success),
        new("Неуспешные (failure)", PipelineStatus.Failed),
        new("Выполняются", PipelineStatus.Running),
        new("Отменённые", PipelineStatus.Cancelled)
    ];

    public ICommand RefreshCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand ToggleAllTimeCommand { get; }

    private DateTime? _periodStartDate = DateTime.Today.AddDays(-29);
    public DateTime? PeriodStartDate
    {
        get => _periodStartDate;
        set
        {
            if (SetField(ref _periodStartDate, value) && !_isUpdatingPeriod)
                IsAllTime = false;
        }
    }

    private DateTime? _periodEndDate = DateTime.Today;
    public DateTime? PeriodEndDate
    {
        get => _periodEndDate;
        set
        {
            if (SetField(ref _periodEndDate, value) && !_isUpdatingPeriod)
                IsAllTime = false;
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
        }
    }

    public bool ArePeriodDatesEnabled => !IsAllTime;
    public string AllTimeButtonText => IsAllTime ? "Выбрать период" : "За всё время";

    private AnalyticsScopeOption? _selectedRepository;
    public AnalyticsScopeOption? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            if (!SetField(ref _selectedRepository, value) || _isUpdatingFilters)
                return;

            QueueWorkflowReload(value?.Id);
        }
    }

    private AnalyticsScopeOption? _selectedWorkflow;
    public AnalyticsScopeOption? SelectedWorkflow
    {
        get => _selectedWorkflow;
        set => SetField(ref _selectedWorkflow, value);
    }

    private PipelineStatus? _selectedStatus;
    public PipelineStatus? SelectedStatus
    {
        get => _selectedStatus;
        set => SetField(ref _selectedStatus, value);
    }

    private MetricsSummaryResponse? _metrics;
    public MetricsSummaryResponse? Metrics
    {
        get => _metrics;
        private set => SetField(ref _metrics, value);
    }

    private IReadOnlyList<AnalyticsComparisonResponse> _repositoryComparisons = [];
    public IReadOnlyList<AnalyticsComparisonResponse> RepositoryComparisons
    {
        get => _repositoryComparisons;
        private set => SetField(ref _repositoryComparisons, value);
    }

    private IReadOnlyList<AnalyticsComparisonResponse> _workflowComparisons = [];
    public IReadOnlyList<AnalyticsComparisonResponse> WorkflowComparisons
    {
        get => _workflowComparisons;
        private set => SetField(ref _workflowComparisons, value);
    }

    private IReadOnlyList<FailureFrequencyResponse> _jobFailures = [];
    public IReadOnlyList<FailureFrequencyResponse> JobFailures
    {
        get => _jobFailures;
        private set => SetField(ref _jobFailures, value);
    }

    private IReadOnlyList<AnalyticsTrendPointViewModel> _trend = [];
    public IReadOnlyList<AnalyticsTrendPointViewModel> Trend
    {
        get => _trend;
        private set
        {
            if (!SetField(ref _trend, value))
                return;

            OnPropertyChanged(nameof(SuccessfulRunValues));
            OnPropertyChanged(nameof(FailedRunValues));
            OnPropertyChanged(nameof(OtherRunValues));
            OnPropertyChanged(nameof(AverageDurationValues));
            OnPropertyChanged(nameof(TrendLabels));
        }
    }

    public IReadOnlyList<int> SuccessfulRunValues => Trend.Select(point => point.SuccessfulRuns).ToArray();
    public IReadOnlyList<int> FailedRunValues => Trend.Select(point => point.FailedRuns).ToArray();
    public IReadOnlyList<int> OtherRunValues => Trend.Select(point => point.OtherRuns).ToArray();
    public IReadOnlyList<double> AverageDurationValues =>
        Trend.Select(point => point.AverageDurationSeconds).ToArray();
    public IReadOnlyList<string> TrendLabels => Trend.Select(point => point.DateLabel).ToArray();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public async Task ActivateAsync(CancellationToken ct = default)
    {
        await LoadRepositoriesAsync(ct);
        if (!ct.IsCancellationRequested)
            await LoadAnalyticsAsync(ct);
    }

    private async Task LoadRepositoriesAsync(CancellationToken ct)
    {
        var selectedId = SelectedRepository?.Id;
        var result = await _apiClient.GetRepositoriesAsync(ct);
        if (!result.IsSuccess)
        {
            StatusMessage = result.ErrorMessage!;
            return;
        }

        _isUpdatingFilters = true;
        try
        {
            Repositories.Clear();
            Repositories.Add(new AnalyticsScopeOption(null, "Все репозитории"));
            foreach (var repository in result.Value!)
                Repositories.Add(new AnalyticsScopeOption(repository.Id, repository.FullName));
            SelectedRepository = Repositories.FirstOrDefault(item => item.Id == selectedId)
                                 ?? Repositories[0];
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        await LoadWorkflowsAsync(SelectedRepository?.Id, ct);
    }

    private async Task LoadAnalyticsAsync(CancellationToken ct = default)
    {
        if (!TryGetPeriod(out var periodStart, out var periodEnd, out var errorMessage))
        {
            StatusMessage = errorMessage;
            return;
        }

        IsLoading = true;
        StatusMessage = "Расчёт аналитики...";
        try
        {
            var result = await _apiClient.GetAnalyticsAsync(
                periodStart,
                periodEnd,
                SelectedRepository?.Id,
                SelectedWorkflow?.Id,
                SelectedStatus,
                IsAllTime,
                ct);
            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage!;
                return;
            }

            var analytics = result.Value!;
            if (IsAllTime)
                UpdatePeriodFromResponse(analytics);
            Metrics = analytics.Metrics;
            RepositoryComparisons = analytics.RepositoryComparisons;
            WorkflowComparisons = analytics.WorkflowComparisons;
            JobFailures = analytics.Metrics.JobFailures;
            Trend = analytics.Trend
                .Select(point => new AnalyticsTrendPointViewModel(point))
                .ToArray();
            StatusMessage = analytics.Metrics.TotalRuns == 0
                ? IsAllTime ? "За всё время запусков нет" : "За выбранный период запусков нет"
                : IsAllTime
                    ? $"Проанализировано запусков за всё время: {analytics.Metrics.TotalRuns}"
                    : $"Проанализировано запусков: {analytics.Metrics.TotalRuns}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetFiltersAsync()
    {
        IsAllTime = false;
        PeriodStartDate = DateTime.Today.AddDays(-29);
        PeriodEndDate = DateTime.Today;
        SelectedStatus = null;
        _workflowLoadCancellation?.Cancel();
        _isUpdatingFilters = true;
        try
        {
            SelectedRepository = Repositories.FirstOrDefault();
        }
        finally
        {
            _isUpdatingFilters = false;
        }
        await LoadWorkflowsAsync(null, CancellationToken.None);
        await LoadAnalyticsAsync();
    }

    private async Task ToggleAllTimeAsync()
    {
        if (!IsAllTime)
        {
            _isUpdatingPeriod = true;
            try
            {
                PeriodEndDate = DateTime.Today;
            }
            finally
            {
                _isUpdatingPeriod = false;
            }

            IsAllTime = true;
            await LoadAnalyticsAsync();
            return;
        }

        IsAllTime = false;
        StatusMessage = "Выберите даты и нажмите «Применить»";
    }

    private void UpdatePeriodFromResponse(AnalyticsResponse analytics)
    {
        _isUpdatingPeriod = true;
        try
        {
            PeriodStartDate = analytics.PeriodStart.LocalDateTime.Date;
            PeriodEndDate = analytics.PeriodEnd.LocalDateTime.Date.AddDays(-1);
        }
        finally
        {
            _isUpdatingPeriod = false;
        }
    }

    private void QueueWorkflowReload(Guid? repositoryId)
    {
        _workflowLoadCancellation?.Cancel();
        _workflowLoadCancellation?.Dispose();
        _workflowLoadCancellation = new CancellationTokenSource();
        _ = LoadWorkflowsAsync(repositoryId, _workflowLoadCancellation.Token);
    }

    private async Task LoadWorkflowsAsync(Guid? repositoryId, CancellationToken ct)
    {
        IReadOnlyList<WorkflowResponse> workflows = [];
        if (repositoryId.HasValue)
        {
            ApiOperationResult<IReadOnlyList<WorkflowResponse>> result;
            try
            {
                result = await _apiClient.GetWorkflowsAsync(repositoryId.Value, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            if (!result.IsSuccess)
            {
                if (!ct.IsCancellationRequested)
                    StatusMessage = result.ErrorMessage!;
                return;
            }

            workflows = result.Value!;
        }

        if (ct.IsCancellationRequested)
            return;

        var selectedId = SelectedWorkflow?.Id;
        _isUpdatingFilters = true;
        try
        {
            Workflows.Clear();
            Workflows.Add(new AnalyticsScopeOption(null, "Все пайплайны"));
            foreach (var workflow in workflows)
                Workflows.Add(new AnalyticsScopeOption(workflow.Id, workflow.Name));
            SelectedWorkflow = Workflows.FirstOrDefault(item => item.Id == selectedId)
                               ?? Workflows[0];
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    private bool TryGetPeriod(
        out DateTimeOffset periodStart,
        out DateTimeOffset periodEnd,
        out string errorMessage)
    {
        periodStart = default;
        periodEnd = default;
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
