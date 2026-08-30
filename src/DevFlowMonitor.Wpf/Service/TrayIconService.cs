using System.ComponentModel;
using System.Drawing;
using System.Windows;
using DevFlowMonitor.Wpf.View;
using Forms = System.Windows.Forms;

namespace DevFlowMonitor.Wpf.Service;

public sealed class TrayIconService(IAppSettingsService settingsService) : ITrayIconService
{
    private MainWindow? _window;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _contextMenu;
    private Forms.ToolStripMenuItem? _pauseItem;
    private bool _exitRequested;

    public void Initialize(MainWindow window)
    {
        _window = window;
        _window.Closing += OnWindowClosing;
        _window.StateChanged += OnWindowStateChanged;

        _pauseItem = new Forms.ToolStripMenuItem();
        _pauseItem.Click += OnPauseNotifications;
        RefreshPauseItem();

        _contextMenu = CreateContextMenu(_pauseItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "DevFlow Monitor",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested || !settingsService.Load().NotificationsEnabled)
            return;

        e.Cancel = true;
        HideWindow();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window?.WindowState == WindowState.Minimized
            && settingsService.Load().NotificationsEnabled)
            HideWindow();
    }

    private void HideWindow()
    {
        if (_window is null)
            return;

        _window.Hide();
        _window.ShowInTaskbar = false;
    }

    private void ShowWindow()
    {
        if (_window is null)
            return;

        _window.Dispatcher.Invoke(() =>
        {
            _window.ShowInTaskbar = true;
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    private void OnPauseNotifications(object? sender, EventArgs e)
    {
        settingsService.Update(settings =>
            settings.NotificationsEnabled = !settings.NotificationsEnabled);
        RefreshPauseItem();
    }

    private Forms.ContextMenuStrip CreateContextMenu(Forms.ToolStripMenuItem pauseItem)
    {
        var openItem = new Forms.ToolStripMenuItem("Открыть DevFlow Monitor");
        openItem.Click += (_, _) => ShowWindow();

        var exitItem = new Forms.ToolStripMenuItem("Выйти из приложения");
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(openItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void RefreshPauseItem()
    {
        if (_pauseItem is null)
            return;

        _pauseItem.Text = settingsService.Load().NotificationsEnabled
            ? "Приостановить уведомления"
            : "Возобновить уведомления";
    }

    private void ExitApplication()
    {
        _exitRequested = true;

        if (_notifyIcon is not null)
            _notifyIcon.Visible = false;

        System.Windows.Application.Current.Dispatcher.Invoke(
            System.Windows.Application.Current.Shutdown);
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnWindowClosing;
            _window.StateChanged -= OnWindowStateChanged;
        }

        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
        _notifyIcon = null;
        _contextMenu = null;
        _pauseItem = null;
        _window = null;
    }
}
