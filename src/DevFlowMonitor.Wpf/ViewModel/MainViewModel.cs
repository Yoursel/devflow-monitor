using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Service;

namespace DevFlowMonitor.Wpf.ViewModel;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private bool _disposed;

    private readonly INavigationService _navigationService;
    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.PropertyChanged += OnNavigationPropertyChanged;

        NavigateToDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo<DashboardViewModel>());
        NavigateToPipelinesCommand = new RelayCommand(() => _navigationService.NavigateTo<PipelinesViewModel>());
        NavigateToSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo<SettingsViewModel>());

        _navigationService.NavigateTo<DashboardViewModel>();
    }

    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToPipelinesCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }

    public object? CurrentViewModel => _navigationService.CurrentViewModel;

    public bool IsDashboardActive  => _navigationService.CurrentViewModel is DashboardViewModel;
    public bool IsPipelinesActive  => _navigationService.CurrentViewModel is PipelinesViewModel;
    public bool IsSettingsActive   => _navigationService.CurrentViewModel is SettingsViewModel;


    #region OnPropertyChanged

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(INavigationService.CurrentViewModel)) return;

        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsPipelinesActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
            _navigationService.PropertyChanged -= OnNavigationPropertyChanged;
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}