using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Wpf.View;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldViewModel)
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newViewModel)
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.GitHubToken)
            && DataContext is SettingsViewModel viewModel
            && PasswordField.Password != viewModel.GitHubToken)
        {
            PasswordField.Password = viewModel.GitHubToken;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            PasswordField.Password = vm.GitHubToken;
        }
    }

    private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
        {
            vm.GitHubToken = pb.Password;
        }
    }
}
