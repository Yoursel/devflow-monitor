using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Dto;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PaginationViewModel : INotifyPropertyChanged
{
    private const int PageWindowSize = 3;

    private readonly int _pageSize;
    private readonly Func<int, Task> _onPageChanged;

    private int _totalItems;
    private int _currentPage = 1;
    private int _pageWindowStart = 1;

    public PaginationViewModel(int pageSize, Func<int, Task> onPageChanged)
    {
        _pageSize = pageSize;
        _onPageChanged = onPageChanged;

        GoToFirstPageCommand = new AsyncRelayCommand(() => Navigate(1), () => CurrentPage > 1);
        GoToLastPageCommand = new AsyncRelayCommand(() => Navigate(TotalPages), () => CurrentPage < TotalPages);
        GoToNextPageCommand = new AsyncRelayCommand(() => Navigate(CurrentPage + 1), () => CurrentPage < TotalPages);
        GoToPreviousPageCommand = new AsyncRelayCommand(() => Navigate(CurrentPage - 1), () => CurrentPage > 1);
        GoToPageCommand = new AsyncRelayCommand<int>(Navigate);
    }

    public ObservableCollection<PageInfo> VisiblePages { get; } = [];

    private int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value)
                return;

            _currentPage = value;
            OnPropertyChanged();
        }
    }

    public string ItemRangeInfo { get; private set; } = string.Empty;

    private int TotalPages => _totalItems > 0
        ? (int)Math.Ceiling((double)_totalItems / _pageSize)
        : 1;

    public AsyncRelayCommand GoToFirstPageCommand { get; }
    public AsyncRelayCommand GoToLastPageCommand { get; }
    public AsyncRelayCommand GoToNextPageCommand { get; }
    public AsyncRelayCommand GoToPreviousPageCommand { get; }
    public AsyncRelayCommand<int> GoToPageCommand { get; }

    public void SetTotalItems(int totalItems, int currentPage)
    {
        _totalItems = totalItems;
        CurrentPage = Math.Clamp(currentPage, 1, TotalPages);
        _pageWindowStart = Math.Clamp(
            CurrentPage - PageWindowSize + 1,
            1,
            Math.Max(1, TotalPages - PageWindowSize + 1));

        Refresh();
    }

    private async Task Navigate(int page)
    {
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        if (CurrentPage < _pageWindowStart)
            _pageWindowStart = CurrentPage;
        else if (CurrentPage >= _pageWindowStart + PageWindowSize)
            _pageWindowStart = CurrentPage - PageWindowSize + 1;

        Refresh();
        await _onPageChanged(CurrentPage);
    }

    private void Refresh()
    {
        VisiblePages.Clear();

        for (var page = _pageWindowStart; page < _pageWindowStart + PageWindowSize && page <= TotalPages; page++)
            VisiblePages.Add(new PageInfo(page, page == CurrentPage));

        var from = _totalItems == 0 ? 0 : (CurrentPage - 1) * _pageSize + 1;
        var to = Math.Min(CurrentPage * _pageSize, _totalItems);
        ItemRangeInfo = $"Показано {from}-{to} из {_totalItems}";

        OnPropertyChanged(nameof(ItemRangeInfo));
        GoToFirstPageCommand.RaiseCanExecuteChanged();
        GoToLastPageCommand.RaiseCanExecuteChanged();
        GoToNextPageCommand.RaiseCanExecuteChanged();
        GoToPreviousPageCommand.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}