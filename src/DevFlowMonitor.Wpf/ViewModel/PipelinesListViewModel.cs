using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Service;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PipelinesListViewModel : INotifyPropertyChanged, IActivatableViewModel
{
    private const int PageSize = 5;

    private readonly IDevFlowApiClient _apiClient;
    private int _currentPage = 1;
    private int _totalItems;

    public PipelinesListViewModel(IDevFlowApiClient apiClient)
    {
        _apiClient = apiClient;
        Pagination = new PaginationViewModel(PageSize, page => LoadPageAsync(page));
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        ApplyFiltersCommand = new AsyncRelayCommand(ApplyFiltersAsync);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        CloseHistoryCommand = new RelayCommand(() => SelectedPipeline = null);
    }

    public ObservableCollection<PipelineViewModel> Pipelines { get; } = [];
    public PaginationViewModel Pagination { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand CloseHistoryCommand { get; }
    public IReadOnlyList<PipelineStatusFilterOption> AvailableStatuses { get; } =
    [
        new("Все статусы", null),
        new("Успешные", PipelineStatus.Success),
        new("С ошибкой", PipelineStatus.Failed),
        new("Выполняются", PipelineStatus.Running),
        new("Отменённые", PipelineStatus.Cancelled)
    ];

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    private string _branchFilter = string.Empty;
    public string BranchFilter
    {
        get => _branchFilter;
        set => SetField(ref _branchFilter, value);
    }

    private PipelineStatus? _selectedStatus;
    public PipelineStatus? SelectedStatus
    {
        get => _selectedStatus;
        set => SetField(ref _selectedStatus, value);
    }

    private PipelineViewModel? _selectedPipeline;
    public PipelineViewModel? SelectedPipeline
    {
        get => _selectedPipeline;
        private set => SetField(ref _selectedPipeline, value);
    }

    public Task ActivateAsync(CancellationToken ct = default) =>
        LoadAsync(ct);

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

    public Task LoadAsync(CancellationToken ct = default) => LoadPageAsync(_currentPage, ct);

    private async Task LoadPageAsync(int page, CancellationToken ct = default)
    {
        IsLoading = true;
        StatusMessage = "Загрузка pipelines...";

        try
        {
            var result = await _apiClient.GetPipelinesAsync(
                page,
                PageSize,
                SearchText,
                BranchFilter,
                SelectedStatus,
                ct);

            if (!result.IsSuccess)
            {
                Pagination.SetTotalItems(_totalItems, _currentPage);
                StatusMessage = result.ErrorMessage!;
                return;
            }

            Pipelines.Clear();

            foreach (var pipeline in result.Items)
                Pipelines.Add(PipelineViewModelMapper.Map(pipeline, ShowHistory));

            _currentPage = page;
            _totalItems = result.TotalItems;
            Pagination.SetTotalItems(_totalItems, _currentPage);
            StatusMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowHistory(PipelineViewModel pipeline) => SelectedPipeline = pipeline;

    private Task ApplyFiltersAsync()
    {
        return LoadFirstPageAsync();
    }

    private Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        BranchFilter = string.Empty;
        SelectedStatus = null;
        return LoadFirstPageAsync();
    }

    private Task LoadFirstPageAsync()
    {
        _currentPage = 1;
        return LoadPageAsync(_currentPage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(name);
    }
}
