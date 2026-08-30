using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Notification;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.ViewModel;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IDevFlowApiClient _apiClient;
    private readonly IDesktopNotificationService _desktopNotifications;
    private bool _isLoadingSettings;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        ILogger<SettingsViewModel> logger,
        IDevFlowApiClient apiClient,
        IDesktopNotificationService desktopNotifications)
    {
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnection);
        SaveCommand = new RelayCommand(Save);
        TestNotificationCommand = new RelayCommand(ShowTestNotification);

        _appSettingsService = appSettingsService;
        _logger = logger;
        _apiClient = apiClient;
        _desktopNotifications = desktopNotifications;

        SetAppSettings();
    }

    private void SetAppSettings()
    {
        var settings = _appSettingsService.Load();

        _isLoadingSettings = true;
        try
        {
            ApiUrl = settings.ApiUrl;
            GitHubProfile = settings.GitHubProfile;
            GitHubToken = settings.GitHubToken;
            NotificationsEnabled = settings.NotificationsEnabled;
            NotifyOnSuccess = settings.NotifyOnSuccess;
            PollingIntervalSeconds = settings.PollingIntervalSeconds;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private string _apiUrl = string.Empty;

    public string ApiUrl
    {
        get => _apiUrl;
        set
        {
            if (SetField(ref _apiUrl, value))
                InvalidateConnectionStatus();
        }
    }

    private string _gitHubProfile = string.Empty;

    public string GitHubProfile
    {
        get => _gitHubProfile;
        set
        {
            if (SetField(ref _gitHubProfile, value))
                InvalidateConnectionStatus();
        }
    }

    private string _gitHubToken = string.Empty;

    public string GitHubToken
    {
        get => _gitHubToken;
        set
        {
            if (SetField(ref _gitHubToken, value))
                InvalidateConnectionStatus();
        }
    }

    private void InvalidateConnectionStatus()
    {
        ConnectionStatus = ConnectionStatus.NotTested;
        ApiStatus = null;
        StatusMessage = string.Empty;
    }

    private bool _notificationsEnabled;
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (SetField(ref _notificationsEnabled, value))
                SaveNotificationSettings();
        }
    }

    private bool _notifyOnSuccess;
    public bool NotifyOnSuccess
    {
        get => _notifyOnSuccess;
        set
        {
            if (SetField(ref _notifyOnSuccess, value))
                SaveNotificationSettings();
        }
    }

    private int _pollingIntervalSeconds = 60;
    public int PollingIntervalSeconds
    {
        get => _pollingIntervalSeconds;
        set
        {
            if (SetField(ref _pollingIntervalSeconds, value))
                SaveNotificationSettings();
        }
    }

    public IReadOnlyList<int> AvailablePollingIntervals { get; } = [30, 60, 120, 300];

    private ConnectionStatus _connectionStatus;
    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private ApiHealthStatus? _apiStatus;
    public ApiHealthStatus? ApiStatus
    {
        get => _apiStatus;
        set => SetField(ref _apiStatus, value);
    }

    public ICommand CheckConnectionCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestNotificationCommand { get; }

    public async Task CheckConnection()
    {
        ConnectionStatus = ConnectionStatus.Testing;
        ApiStatus = null;
        StatusMessage = "Проверка соединения...";

        var result = await _apiClient.CheckConnectionAsync(ApiUrl, GitHubProfile, GitHubToken);

        ConnectionStatus = result.ConnectionStatus;
        ApiStatus = result.ApiStatus;
        StatusMessage = result.Message;
    }

    private void Save()
    {
        try
        {
            _appSettingsService.Save(CreateCurrentSettings());

            StatusMessage = ConnectionStatus == ConnectionStatus.Connected
                ? "Настройки успешно сохранены!"
                : "Настройки сохранены, но соединение не проверено";
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or CryptographicException)
        {
            ConnectionStatus = ConnectionStatus.Failed;
            StatusMessage = $"Не удалось сохранить: {ex.Message}";
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    private void ShowTestNotification()
    {
        try
        {
            _desktopNotifications.Show(new PipelineNotification(
                0,
                "Тестовый pipeline",
                "main",
                PipelineStatus.Success));
            StatusMessage = "Тестовое уведомление отправлено";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось показать уведомление: {ex.Message}";
            _logger.LogError(ex, "Failed to show test desktop notification");
        }
    }

    private void SaveNotificationSettings()
    {
        if (_isLoadingSettings)
            return;

        try
        {
            _appSettingsService.Update(settings =>
            {
                settings.NotificationsEnabled = NotificationsEnabled;
                settings.NotifyOnSuccess = NotifyOnSuccess;
                settings.PollingIntervalSeconds = PollingIntervalSeconds;
            });
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or CryptographicException)
        {
            StatusMessage = $"Не удалось сохранить настройки уведомлений: {ex.Message}";
            _logger.LogError(ex, "Failed to save notification settings");
        }
    }

    private AppSettings CreateCurrentSettings() => new()
    {
        ApiUrl = ApiUrl,
        GitHubProfile = GitHubProfile,
        GitHubToken = GitHubToken,
        NotificationsEnabled = NotificationsEnabled,
        NotifyOnSuccess = NotifyOnSuccess,
        PollingIntervalSeconds = PollingIntervalSeconds,
    };

    #region OnPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    #endregion
}
