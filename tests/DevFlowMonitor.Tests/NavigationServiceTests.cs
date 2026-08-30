using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevFlowMonitor.Tests;

public class NavigationServiceTests
{
    [Fact]
    public async Task NavigateTo_ActivatesViewModelWhenSupported()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ActivatableViewModel>();
        var provider = services.BuildServiceProvider();
        var navigationService = new NavigationService(
            provider,
            NullLogger<NavigationService>.Instance);

        navigationService.NavigateTo<ActivatableViewModel>();

        await WaitUntil(() => provider.GetRequiredService<ActivatableViewModel>().ActivationCount == 1);
        Assert.Equal(1, provider.GetRequiredService<ActivatableViewModel>().ActivationCount);
        Assert.Same(
            provider.GetRequiredService<ActivatableViewModel>(),
            navigationService.CurrentViewModel);
    }

    [Fact]
    public async Task NavigateTo_CancelsPreviousActivation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SlowActivatableViewModel>();
        services.AddSingleton<ActivatableViewModel>();
        var provider = services.BuildServiceProvider();
        var navigationService = new NavigationService(
            provider,
            NullLogger<NavigationService>.Instance);

        navigationService.NavigateTo<SlowActivatableViewModel>();
        var slowViewModel = provider.GetRequiredService<SlowActivatableViewModel>();
        await WaitUntil(() => slowViewModel.ActivationStarted);

        navigationService.NavigateTo<ActivatableViewModel>();

        await WaitUntil(() => slowViewModel.ActivationToken.IsCancellationRequested);
        Assert.True(slowViewModel.ActivationToken.IsCancellationRequested);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class ActivatableViewModel : IActivatableViewModel
    {
        public int ActivationCount { get; private set; }

        public Task ActivateAsync(CancellationToken ct = default)
        {
            ActivationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SlowActivatableViewModel : IActivatableViewModel
    {
        public bool ActivationStarted { get; private set; }
        public CancellationToken ActivationToken { get; private set; }

        public async Task ActivateAsync(CancellationToken ct = default)
        {
            ActivationToken = ct;
            ActivationStarted = true;
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
