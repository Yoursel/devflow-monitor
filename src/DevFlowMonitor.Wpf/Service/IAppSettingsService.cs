using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.Service;

public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}