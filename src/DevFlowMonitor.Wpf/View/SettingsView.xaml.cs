using System.Windows;
using System.Windows.Controls;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Wpf.View;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            PasswordField.Password = vm.Password;
        }
    }

    private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
        {
            vm.Password = pb.Password;
        }
    }
}