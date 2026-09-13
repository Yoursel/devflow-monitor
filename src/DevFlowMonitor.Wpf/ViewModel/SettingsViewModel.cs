using System.ComponentModel;
using System.Collections.ObjectModel;
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
    private readonly AsyncRelayCommand _synchronizeAccountCommand;
    private readonly AsyncRelayCommand _deleteAccountCommand;
    private bool _isLoadingSettings;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        ILogger<SettingsViewModel> logger,
        IDevFlowApiClient apiClient,
        IDesktopNotificationService desktopNotifications)
    {
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync);
        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync);
        _synchronizeAccountCommand = new AsyncRelayCommand(
            SynchronizeAccountAsync,
            () => HasRegisteredGitHubAccount);
        _deleteAccountCommand = new AsyncRelayCommand(
            DeleteAccountAsync,
            () => HasRegisteredGitHubAccount);
        SynchronizeAccountCommand = _synchronizeAccountCommand;
        DeleteAccountCommand = _deleteAccountCommand;
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
            GitHubAccounts.Clear();
            foreach (var account in settings.GitHubAccounts.Where(account => account.Id != Guid.Empty))
                GitHubAccounts.Add(account);
            SelectedGitHubAccount = GitHubAccounts.FirstOrDefault(account =>
                                           account.Id == settings.ActiveGitHubAccountId)
                                       ?? GitHubAccounts.FirstOrDefault();
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

    public ObservableCollection<GitHubAccountSettings> GitHubAccounts { get; } = [];

    private GitHubAccountSettings? _selectedGitHubAccount;
    public bool HasRegisteredGitHubAccount =>
        SelectedGitHubAccount is { Id: var accountId } && accountId != Guid.Empty;

    public GitHubAccountSettings? SelectedGitHubAccount
    {
        get => _selectedGitHubAccount;
        set
        {
            if (!SetField(ref _selectedGitHubAccount, value))
                return;

            OnPropertyChanged(nameof(HasRegisteredGitHubAccount));
            _synchronizeAccountCommand.RaiseCanExecuteChanged();
            _deleteAccountCommand.RaiseCanExecuteChanged();

            if (value is null)
                return;

            GitHubProfile = value.Owner;
            GitHubToken = value.Token;
        }
    }

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

    private string _notificationStatusMessage = string.Empty;
    public string NotificationStatusMessage
    {
        get => _notificationStatusMessage;
        set => SetField(ref _notificationStatusMessage, value);
    }

    private ApiHealthStatus? _apiStatus;
    public ApiHealthStatus? ApiStatus
    {
        get => _apiStatus;
        set => SetField(ref _apiStatus, value);
    }

    public ICommand CheckConnectionCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand SynchronizeAccountCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestNotificationCommand { get; }

    public async Task CheckConnectionAsync()
    {
        ConnectionStatus = ConnectionStatus.Testing;
        ApiStatus = null;
        StatusMessage = "Проверка соединения...";

        var result = await _apiClient.CheckConnectionAsync(ApiUrl, GitHubProfile, GitHubToken);

        ConnectionStatus = result.ConnectionStatus;
        ApiStatus = result.ApiStatus;
        StatusMessage = result.Message;
    }

    private async Task AddAccountAsync()
    {
        StatusMessage = "Добавление GitHub-аккаунта...";
        var result = await _apiClient.AddGitHubAccountAsync(
            ApiUrl,
            GitHubProfile,
            GitHubToken);

        if (!result.IsSuccess)
        {
            ConnectionStatus = ConnectionStatus.Failed;
            StatusMessage = result.ErrorMessage!;
            return;
        }

        var response = result.Value!;
        var localAccount = GitHubAccounts.FirstOrDefault(account => account.Id == response.Id)
            ?? GitHubAccounts.FirstOrDefault(account =>
                account.Id == Guid.Empty
                && string.Equals(account.Owner, response.Owner, StringComparison.OrdinalIgnoreCase));
        if (localAccount is null)
        {
            localAccount = new GitHubAccountSettings { Id = response.Id };
            GitHubAccounts.Add(localAccount);
        }

        localAccount.Owner = response.Owner;
        localAccount.Token = GitHubToken;
        localAccount.LastSynchronizedAt = response.LastSynchronizedAt;
        SelectedGitHubAccount = localAccount;
        SaveCurrentSettings();
        ConnectionStatus = ConnectionStatus.Connected;
        await SynchronizeAccountAsync();
    }

    private async Task SynchronizeAccountAsync()
    {
        if (SelectedGitHubAccount is not { Id: var accountId } || accountId == Guid.Empty)
        {
            StatusMessage = "Сначала добавьте аккаунт";
            return;
        }

        StatusMessage = $"Синхронизация {SelectedGitHubAccount.Owner}...";
        var result = await _apiClient.SynchronizeGitHubAccountAsync(
            ApiUrl,
            accountId,
            GitHubToken);

        if (!result.IsSuccess)
        {
            StatusMessage = result.ErrorMessage!;
            return;
        }

        var summary = result.Value!;
        SelectedGitHubAccount.Token = GitHubToken;
        SelectedGitHubAccount.LastSynchronizedAt = summary.SynchronizedAt;
        SaveCurrentSettings();
        StatusMessage = $"Синхронизировано: {summary.Repositories} реп., {summary.Runs} запусков, job: {summary.Jobs}";
    }

    private async Task DeleteAccountAsync()
    {
        if (SelectedGitHubAccount is null)
        {
            StatusMessage = "Выберите аккаунт";
            return;
        }

        var account = SelectedGitHubAccount;
        if (account.Id != Guid.Empty)
        {
            var result = await _apiClient.DeleteGitHubAccountAsync(ApiUrl, account.Id);
            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage!;
                return;
            }
        }

        GitHubAccounts.Remove(account);
        SelectedGitHubAccount = GitHubAccounts.FirstOrDefault();
        if (SelectedGitHubAccount is null)
        {
            GitHubProfile = string.Empty;
            GitHubToken = string.Empty;
        }

        SaveCurrentSettings();
        StatusMessage = "GitHub-аккаунт удалён";
    }

    private void Save()
    {
        try
        {
            if (SelectedGitHubAccount is not null)
            {
                SelectedGitHubAccount.Owner = GitHubProfile;
                SelectedGitHubAccount.Token = GitHubToken;
            }

            SaveCurrentSettings();

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
                "Тестовый пайплайн",
                "main",
                PipelineStatus.Success));
            NotificationStatusMessage = "Тестовое уведомление отправлено";
        }
        catch (Exception ex)
        {
            NotificationStatusMessage = $"Не удалось показать уведомление: {ex.Message}";
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
            NotificationStatusMessage = $"Не удалось сохранить настройки уведомлений: {ex.Message}";
            _logger.LogError(ex, "Failed to save notification settings");
        }
    }

    private AppSettings CreateCurrentSettings() => new()
    {
        ApiUrl = ApiUrl,
        GitHubProfile = GitHubProfile,
        GitHubToken = GitHubToken,
        ActiveGitHubAccountId = SelectedGitHubAccount?.Id,
        GitHubAccounts = GitHubAccounts.Select(account => new GitHubAccountSettings
        {
            Id = account.Id,
            Owner = account.Owner,
            Token = account.Token,
            LastSynchronizedAt = account.LastSynchronizedAt
        }).ToList(),
        NotificationsEnabled = NotificationsEnabled,
        NotifyOnSuccess = NotifyOnSuccess,
        PollingIntervalSeconds = PollingIntervalSeconds,
    };

    private void SaveCurrentSettings() =>
        _appSettingsService.Save(CreateCurrentSettings());

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
