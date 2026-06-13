using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Service;
using DevFlowMonitor.Wpf.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevFlowMonitor.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task CheckConnection_SetsSuccessStatus()
    {
        var apiClient = new StubApiClient
        {
            Result = new ConnectionCheckResult(
                ConnectionStatus.Connected,
                "Соединение установлено. API v1.2.3",
                ApiHealthStatus.Healthy,
                "1.2.3")
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.ApiUrl = "http://localhost:5268";

        await viewModel.CheckConnection();

        Assert.Equal(ConnectionStatus.Connected, viewModel.ConnectionStatus);
        Assert.Equal(ApiHealthStatus.Healthy, viewModel.ApiStatus);
        Assert.Contains("1.2.3", viewModel.StatusMessage);
        Assert.Equal("http://localhost:5268", apiClient.LastApiUrl);
    }

    [Fact]
    public async Task CheckConnection_SetsFailedStatusWhenClientRejectsUrl()
    {
        var apiClient = new StubApiClient
        {
            Result = new ConnectionCheckResult(
                ConnectionStatus.Failed,
                "URL API не задан")
        };
        var viewModel = CreateViewModel(apiClient);

        await viewModel.CheckConnection();

        Assert.Equal(ConnectionStatus.Failed, viewModel.ConnectionStatus);
        Assert.Equal("URL API не задан", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EditingSettings_InvalidatesSuccessfulConnection()
    {
        var viewModel = CreateViewModel(new StubApiClient
        {
            Result = new ConnectionCheckResult(
                ConnectionStatus.Connected,
                "Соединение установлено. API v1.2.3",
                ApiHealthStatus.Healthy,
                "1.2.3")
        });
        viewModel.ApiUrl = "http://localhost:5268";
        await viewModel.CheckConnection();

        viewModel.Username = "new-user";

        Assert.Equal(ConnectionStatus.NotTested, viewModel.ConnectionStatus);
        Assert.Null(viewModel.ApiStatus);
        Assert.Empty(viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_PersistsCurrentSettings()
    {
        var settingsService = new StubSettingsService();
        var viewModel = CreateViewModel(new StubApiClient(), settingsService);
        viewModel.ApiUrl = "http://localhost:5268";
        viewModel.Username = "user";
        viewModel.Password = "secret";

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("http://localhost:5268", settingsService.SavedSettings.ApiUrl);
        Assert.Equal("user", settingsService.SavedSettings.Username);
        Assert.Equal("secret", settingsService.SavedSettings.Password);
    }

    private static SettingsViewModel CreateViewModel(
        StubApiClient apiClient,
        StubSettingsService? settingsService = null)
    {
        return new SettingsViewModel(
            settingsService ?? new StubSettingsService(),
            NullLogger<SettingsViewModel>.Instance,
            apiClient);
    }

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public ConnectionCheckResult Result { get; init; } =
            new(ConnectionStatus.Connected, "Соединение установлено");

        public string? LastApiUrl { get; private set; }

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl,
            CancellationToken ct = default)
        {
            LastApiUrl = apiUrl;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubSettingsService : IAppSettingsService
    {
        public AppSettings? SavedSettings { get; private set; }

        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
            SavedSettings = settings;
        }
    }
}
