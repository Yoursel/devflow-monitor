using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevFlowMonitor.Wpf.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Service;

public class NavigationService(
    IServiceProvider serviceProvider,
    ILogger<NavigationService> logger)
    : INavigationService, INotifyPropertyChanged, IDisposable
{
    private object? _currentViewModel;
    private CancellationTokenSource? _activationCts;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set { _currentViewModel = value; OnPropertyChanged(); }
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var viewModel = serviceProvider.GetRequiredService<TViewModel>();
        var activationCts = ResetActivation();

        CurrentViewModel = viewModel;
        _ = ActivateAsync(viewModel, activationCts);
    }

    private CancellationTokenSource ResetActivation()
    {
        _activationCts?.Cancel();
        _activationCts = new CancellationTokenSource();

        return _activationCts;
    }

    private async Task ActivateAsync(object viewModel, CancellationTokenSource activationCts)
    {
        var ct = activationCts.Token;

        try
        {
            if (viewModel is not IActivatableViewModel activatableViewModel)
                return;

            await activatableViewModel.ActivateAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to activate view model {ViewModelType}",
                viewModel.GetType().Name);
        }
        finally
        {
            if (!ReferenceEquals(_activationCts, activationCts))
                activationCts.Dispose();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _activationCts?.Cancel();
        _activationCts?.Dispose();
    }
}