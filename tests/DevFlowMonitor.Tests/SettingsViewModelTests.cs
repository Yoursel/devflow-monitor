using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Model;
using DevFlowMonitor.Wpf.Notification;
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
        viewModel.GitHubProfile = "Yoursel";
        viewModel.GitHubToken = "github_pat_test";

        await viewModel.CheckConnection();

        Assert.Equal(ConnectionStatus.Connected, viewModel.ConnectionStatus);
        Assert.Equal(ApiHealthStatus.Healthy, viewModel.ApiStatus);
        Assert.Contains("1.2.3", viewModel.StatusMessage);
        Assert.Equal("http://localhost:5268", apiClient.LastApiUrl);
        Assert.Equal("Yoursel", apiClient.LastGitHubProfile);
        Assert.Equal("github_pat_test", apiClient.LastGitHubToken);
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

        viewModel.GitHubToken = "new-token";

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
        viewModel.GitHubProfile = "Yoursel";
        viewModel.GitHubToken = "secret";
        viewModel.NotificationsEnabled = true;
        viewModel.NotifyOnSuccess = true;
        viewModel.PollingIntervalSeconds = 120;

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal("http://localhost:5268", settingsService.SavedSettings.ApiUrl);
        Assert.Equal("Yoursel", settingsService.SavedSettings.GitHubProfile);
        Assert.Equal("secret", settingsService.SavedSettings.GitHubToken);
        Assert.True(settingsService.SavedSettings.NotificationsEnabled);
        Assert.True(settingsService.SavedSettings.NotifyOnSuccess);
        Assert.Equal(120, settingsService.SavedSettings.PollingIntervalSeconds);
    }

    [Fact]
    public void ChangingNotificationSettings_PersistsThemImmediately()
    {
        var settingsService = new StubSettingsService();
        var viewModel = CreateViewModel(new StubApiClient(), settingsService);

        Assert.Null(settingsService.SavedSettings);

        viewModel.NotificationsEnabled = true;
        viewModel.NotifyOnSuccess = true;
        viewModel.PollingIntervalSeconds = 120;

        Assert.NotNull(settingsService.SavedSettings);
        Assert.True(settingsService.SavedSettings.NotificationsEnabled);
        Assert.True(settingsService.SavedSettings.NotifyOnSuccess);
        Assert.Equal(120, settingsService.SavedSettings.PollingIntervalSeconds);
    }

    [Fact]
    public void TestNotificationCommand_ShowsDesktopNotification()
    {
        var notificationService = new StubNotificationService();
        var viewModel = new SettingsViewModel(
            new StubSettingsService(),
            NullLogger<SettingsViewModel>.Instance,
            new StubApiClient(),
            notificationService);

        viewModel.TestNotificationCommand.Execute(null);

        Assert.NotNull(notificationService.LastNotification);
        Assert.Equal(PipelineStatus.Success, notificationService.LastNotification.Status);
        Assert.Equal("Тестовое уведомление отправлено", viewModel.StatusMessage);
    }

    private static SettingsViewModel CreateViewModel(
        StubApiClient apiClient,
        StubSettingsService? settingsService = null)
    {
        return new SettingsViewModel(
            settingsService ?? new StubSettingsService(),
            NullLogger<SettingsViewModel>.Instance,
            apiClient,
            new StubNotificationService());
    }

    private sealed class StubApiClient : IDevFlowApiClient
    {
        public ConnectionCheckResult Result { get; init; } =
            new(ConnectionStatus.Connected, "Соединение установлено");

        public string? LastApiUrl { get; private set; }
        public string? LastGitHubProfile { get; private set; }
        public string? LastGitHubToken { get; private set; }

        public Task<ConnectionCheckResult> CheckConnectionAsync(
            string apiUrl,
            string gitHubProfile,
            string gitHubToken,
            CancellationToken ct = default)
        {
            LastApiUrl = apiUrl;
            LastGitHubProfile = gitHubProfile;
            LastGitHubToken = gitHubToken;
            return Task.FromResult(Result);
        }

        public Task<DashboardLoadResult> GetDashboardAsync(CancellationToken ct = default) =>
            Task.FromResult(DashboardLoadResult.Failed("Not configured"));

        public Task<PipelinesLoadResult> GetPipelinesAsync(
            int page,
            int pageSize,
            string? search = null,
            string? branch = null,
            PipelineStatus? status = null,
            CancellationToken ct = default) =>
            Task.FromResult(PipelinesLoadResult.Failed("Not configured"));
    }

    private sealed class StubSettingsService : IAppSettingsService
    {
        public AppSettings? SavedSettings { get; private set; }

        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
            SavedSettings = settings;
        }

        public void Update(Action<AppSettings> update)
        {
            var settings = SavedSettings ?? Load();
            update(settings);
            Save(settings);
        }
    }

    private sealed class StubNotificationService : IDesktopNotificationService
    {
        public PipelineNotification? LastNotification { get; private set; }

        public void Show(PipelineNotification notification)
        {
            LastNotification = notification;
        }
    }
}
