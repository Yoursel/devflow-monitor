using System.ComponentModel;

namespace DevFlowMonitor.Wpf.Service;

public interface INavigationService : INotifyPropertyChanged
{
    object? CurrentViewModel { get; }
    void NavigateTo<TViewModel>() where TViewModel : class;
}