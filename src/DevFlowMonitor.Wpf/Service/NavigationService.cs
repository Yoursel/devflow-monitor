using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlowMonitor.Wpf.Service;

public class NavigationService(IServiceProvider serviceProvider) : INavigationService, INotifyPropertyChanged
{
    private object? _currentViewModel;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set { _currentViewModel = value; OnPropertyChanged(); }
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        CurrentViewModel = serviceProvider.GetRequiredService<TViewModel>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}