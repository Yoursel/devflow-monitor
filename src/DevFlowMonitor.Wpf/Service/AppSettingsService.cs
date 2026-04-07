using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlowMonitor.Wpf.Dto;
using DevFlowMonitor.Wpf.Model;
using Microsoft.Extensions.Logging;

namespace DevFlowMonitor.Wpf.Service;

public class AppSettingsService : IAppSettingsService
{
    private readonly ILogger<AppSettingsService> _logger;

    private const string AppFolderName = "DevFlowMonitor";
    private const string SettingsFileName = "settings.json";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DevFlowMonitor.v1");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, SettingsFileName);

        _logger.LogDebug("Settings file path resolved to {FilePath}", _filePath);
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation(
                "Settings file not found at {FilePath}, returning defaults",
                _filePath);

            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<AppSettingsPersistenceDto>(json);

            if (dto is null)
            {
                _logger.LogWarning(
                    "Settings file at {FilePath} deserialized to null, returning defaults",
                    _filePath);
                return new AppSettings();
            }

            _logger.LogDebug("Settings loaded successfully from {FilePath}", _filePath);

            return new AppSettings
            {
                ApiUrl = dto.ApiUrl,
                Username = dto.Username,
                Password = Unprotect(dto.ProtectedPassword)
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Settings file at {FilePath} is corrupted (invalid JSON), returning defaults",
                _filePath);
            
            return new AppSettings();
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to decrypt password in {FilePath} (file may be from another machine or user), returning defaults",
                _filePath);
            
            return new AppSettings();
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(
                ex,
                "Protected password in {FilePath} is not valid base64, returning defaults",
                _filePath);
            
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dto = new AppSettingsPersistenceDto
        {
            ApiUrl = settings.ApiUrl,
            Username = settings.Username,
            ProtectedPassword = Protect(settings.Password)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_filePath, json);

        _logger.LogInformation("Settings saved to {FilePath}", _filePath);
    }


    private static string? Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(
            bytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
        {
            return string.Empty;
        }

        var protectedBytes = Convert.FromBase64String(protectedBase64);
        var bytes = ProtectedData.Unprotect(
            protectedBytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }
}