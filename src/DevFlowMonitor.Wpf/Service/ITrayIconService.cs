using DevFlowMonitor.Wpf.View;

namespace DevFlowMonitor.Wpf.Service;

public interface ITrayIconService : IDisposable
{
    void Initialize(MainWindow window);
}
