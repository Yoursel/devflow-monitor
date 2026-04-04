using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Dto;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PaginationViewModel : INotifyPropertyChanged
{
    private const int PageWindowSize = 3;

    private readonly int _pageSize;
    private readonly Action<int> _onPageChanged;

    private int _totalItems;
    private int _currentPage = 1;
    private int _pageWindowStart = 1;

    public PaginationViewModel(int pageSize, Action<int> onPageChanged)
    {
        _pageSize = pageSize;
        _onPageChanged = onPageChanged;

        GoToFirstPageCommand = new RelayCommand(() => Navigate(1), () => CurrentPage > 1);
        GoToLastPageCommand = new RelayCommand(() => Navigate(TotalPages), () => CurrentPage < TotalPages);
        NextPageSetCommand = new RelayCommand(() => ShiftPageWindow(1), () => _pageWindowStart + PageWindowSize <= TotalPages);
        PreviousPageSetCommand = new RelayCommand(() => ShiftPageWindow(-1), () => _pageWindowStart > 1);
        GoToPageCommand = new RelayCommand<int>(Navigate);
    }

    public ObservableCollection<PageInfo> VisiblePages { get; } = [];

    private int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value) return;
            _currentPage = value;
            OnPropertyChanged();
        }
    }

    public string ItemRangeInfo { get; private set; } = "";

    private int TotalPages => _totalItems > 0
        ? (int)Math.Ceiling((double)_totalItems / _pageSize)
        : 1;

    public ICommand GoToFirstPageCommand { get; }
    public ICommand GoToLastPageCommand { get; }
    public ICommand NextPageSetCommand { get; }
    public ICommand PreviousPageSetCommand { get; }
    public ICommand GoToPageCommand { get; }

    public void SetTotalItems(int totalItems)
    {
        _totalItems = totalItems;
        _pageWindowStart = 1;
        Navigate(1);
    }

    private void Navigate(int page)
    {
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        if (CurrentPage < _pageWindowStart)
            _pageWindowStart = CurrentPage;
        else if (CurrentPage >= _pageWindowStart + PageWindowSize)
            _pageWindowStart = CurrentPage - PageWindowSize + 1;

        Refresh();
        _onPageChanged(CurrentPage);
    }

    private void ShiftPageWindow(int direction)
    {
        _pageWindowStart = Math.Clamp(
            _pageWindowStart + direction * PageWindowSize,
            1,
            Math.Max(1, TotalPages - PageWindowSize + 1));

        Navigate(_pageWindowStart);
    }

    private void Refresh()
    {
        VisiblePages.Clear();

        for (var i = _pageWindowStart; i < _pageWindowStart + PageWindowSize && i <= TotalPages; i++)
            VisiblePages.Add(new PageInfo(i, i == CurrentPage));

        var from = (CurrentPage - 1) * _pageSize + 1;
        var to = Math.Min(CurrentPage * _pageSize, _totalItems);
        ItemRangeInfo = $"С {from} по {to} запись из {_totalItems}";

        OnPropertyChanged(nameof(ItemRangeInfo));
    }

    #region OnPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}