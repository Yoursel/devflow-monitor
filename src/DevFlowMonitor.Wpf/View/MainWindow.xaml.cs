using System.Windows;
using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Wpf.View;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        
        DataContext = viewModel;
    }
}