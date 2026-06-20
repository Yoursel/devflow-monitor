using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
    }

    public ObservableCollection<PipelineViewModel> Pipelines { get; } = [];
    public PaginationViewModel Pagination { get; }
    public ICommand RefreshCommand { get; }

    public Task ActivateAsync(CancellationToken ct = default) =>
        LoadAsync(ct);

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

    public Task LoadAsync(CancellationToken ct = default) => LoadPageAsync(_currentPage, ct);

    private async Task LoadPageAsync(int page, CancellationToken ct = default)
    {
        IsLoading = true;
        StatusMessage = "Загрузка pipelines...";

        try
        {
            var result = await _apiClient.GetPipelinesAsync(page, PageSize, ct);

            if (!result.IsSuccess)
            {
                Pagination.SetTotalItems(_totalItems, _currentPage);
                StatusMessage = result.ErrorMessage!;
                return;
            }

            Pipelines.Clear();

            foreach (var pipeline in result.Items)
                Pipelines.Add(PipelineViewModelMapper.Map(pipeline));

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}