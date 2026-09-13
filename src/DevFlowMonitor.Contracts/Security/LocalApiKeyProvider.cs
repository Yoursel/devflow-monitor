using System.Security.Cryptography;
using System.Text;

namespace DevFlowMonitor.Contracts.Security;

public sealed class LocalApiKeyProvider : ILocalApiKeyProvider
{
    private const string AppFolderName = "DevFlowMonitor";
    private const string KeyFileName = "local-api-key.dat";
    private static readonly byte[] Entropy = "DevFlowMonitor.LocalApi.v1"u8.ToArray();
    private readonly Lock _syncRoot = new();
    private readonly string _filePath;
    private string? _cachedKey;

    public LocalApiKeyProvider()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, KeyFileName);
    }

    public string GetApiKey()
    {
        lock (_syncRoot)
            return _cachedKey ??= LoadOrCreate();
    }

    private string LoadOrCreate()
    {
        if (File.Exists(_filePath))
            return Unprotect(File.ReadAllText(_filePath));

        var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, Protect(apiKey));
            try
            {
                File.Move(temporaryPath, _filePath);
                return apiKey;
            }
            catch (IOException) when (File.Exists(_filePath))
            {
                return Unprotect(File.ReadAllText(_filePath));
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string Protect(string value) => Convert.ToBase64String(
        ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            Entropy,
            DataProtectionScope.CurrentUser));

    private static string Unprotect(string value) => Encoding.UTF8.GetString(
        ProtectedData.Unprotect(
            Convert.FromBase64String(value),
            Entropy,
            DataProtectionScope.CurrentUser));
}
