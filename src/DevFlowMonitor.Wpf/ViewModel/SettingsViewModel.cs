using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.ViewModel;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IDevFlowApiClient _apiClient;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        ILogger<SettingsViewModel> logger,
        IDevFlowApiClient apiClient)
    {
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnection);
        SaveCommand = new RelayCommand(Save);

        _appSettingsService = appSettingsService;
        _logger = logger;
        _apiClient = apiClient;

        SetAppSettings();
    }

    private void SetAppSettings()
    {
        var settings = _appSettingsService.Load();

        ApiUrl = settings.ApiUrl;
        Username = settings.Username;
        Password = settings.Password;
    }

    private string _apiUrl = string.Empty;

    public string ApiUrl
    {
        get => _apiUrl;
        set
        {
            if (_apiUrl == value)
                return;

            _apiUrl = value;
            OnPropertyChanged();
            InvalidateConnectionStatus();
        }
    }

    private string _username = string.Empty;

    public string Username
    {
        get => _username;
        set
        {
            if (_username == value)
                return;
            _username = value;
            OnPropertyChanged();
            InvalidateConnectionStatus();
        }
    }

    private string _password = string.Empty;

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value)
                return;

            _password = value;
            OnPropertyChanged();
            InvalidateConnectionStatus();
        }
    }

    private void InvalidateConnectionStatus()
    {
        ConnectionStatus = ConnectionStatus.NotTested;
        ApiStatus = null;
        StatusMessage = string.Empty;
    }

    private ConnectionStatus _connectionStatus;
    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set
        {
            if (_connectionStatus == value)
                return;
            
            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value)
                return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private ApiHealthStatus? _apiStatus;
    public ApiHealthStatus? ApiStatus
    {
        get => _apiStatus;
        set
        {
            if (_apiStatus == value)
                return;

            _apiStatus = value;
            OnPropertyChanged();
        }
    }

    public ICommand CheckConnectionCommand { get; }
    public ICommand SaveCommand { get; }

    public async Task CheckConnection()
    {
        ConnectionStatus = ConnectionStatus.Testing;
        ApiStatus = null;
        StatusMessage = "Проверка соединения...";

        var result = await _apiClient.CheckConnectionAsync(ApiUrl);

        ConnectionStatus = result.ConnectionStatus;
        ApiStatus = result.ApiStatus;
        StatusMessage = result.Message;
    }

    private void Save()
    {
        try
        {
            _appSettingsService.Save(new AppSettings()
            {
                ApiUrl = ApiUrl,
                Username = Username,
                Password = Password,
            });

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

    #region OnPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}
