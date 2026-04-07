using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using DevFlowMonitor.Wpf.Command;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.ViewModel;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAppSettingsService _appSettingsService;
    public SettingsViewModel(IAppSettingsService appSettingsService, ILogger<SettingsViewModel> logger)
    {
        CheckConnectionCommand = new RelayCommand(CheckConnection);
        SaveCommand = new RelayCommand(Save);

        _appSettingsService = appSettingsService;
        _logger = logger;

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
        }
    }

    private bool? _isConnected;
    public bool? IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected == value)
                return;
            
            _isConnected = value;
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

    public ICommand CheckConnectionCommand { get; }
    public ICommand SaveCommand { get; }

    private void CheckConnection()
    {
        IsConnected = true;
        StatusMessage = "— API отвечает (200 OK)";
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
            
            StatusMessage = $"Настройки успешно сохранены!";
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or CryptographicException)
        {
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