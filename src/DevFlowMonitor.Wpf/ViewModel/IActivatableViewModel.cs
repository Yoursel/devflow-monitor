namespace DevFlowMonitor.Wpf.ViewModel;

public interface IActivatableViewModel
{
    Task ActivateAsync(CancellationToken ct = default);
}