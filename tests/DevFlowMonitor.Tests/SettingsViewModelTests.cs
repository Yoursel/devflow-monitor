using DevFlowMonitor.Contracts;
using DevFlowMonitor.Wpf.Command;
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

        await viewModel.CheckConnectionAsync();

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

        await viewModel.CheckConnectionAsync();

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
        await viewModel.CheckConnectionAsync();

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
        Assert.Equal("Тестовое уведомление отправлено", viewModel.NotificationStatusMessage);
        Assert.Empty(viewModel.StatusMessage);
    }

    [Fact]
    public async Task AddAccountCommand_PersistsAccountAndSynchronizesHistory()
    {
        var accountId = Guid.NewGuid();
        var synchronizedAt = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        var apiClient = new StubApiClient
        {
            AddAccountResult = ApiOperationResult<GitHubAccountResponse>.Success(
                new GitHubAccountResponse(accountId, "Yoursel", 2, synchronizedAt.AddMinutes(-1), null)),
            SyncResult = ApiOperationResult<GitHubSyncResponse>.Success(
                new GitHubSyncResponse(accountId, 2, 3, 10, 11, 12, 30, synchronizedAt, []))
        };
        var settingsService = new StubSettingsService();
        var viewModel = CreateViewModel(apiClient, settingsService);
        viewModel.ApiUrl = "http://localhost:5268";
        viewModel.GitHubProfile = "Yoursel";
        viewModel.GitHubToken = "github_pat_test";

        await ((AsyncRelayCommand)viewModel.AddAccountCommand).ExecuteAsync();

        var account = Assert.Single(viewModel.GitHubAccounts);
        Assert.Equal(accountId, account.Id);
        Assert.Equal("Yoursel", account.Owner);
        Assert.Equal("github_pat_test", account.Token);
        Assert.Equal(synchronizedAt, account.LastSynchronizedAt);
        Assert.Equal(accountId, settingsService.SavedSettings!.ActiveGitHubAccountId);
        Assert.Contains("10 запусков", viewModel.StatusMessage);
        Assert.True(viewModel.SynchronizeAccountCommand.CanExecute(null));
        Assert.True(viewModel.DeleteAccountCommand.CanExecute(null));
    }

    [Fact]
    public void LegacyPlaceholderAccount_IsNotPresentedAsRegisteredAccount()
    {
        var settingsService = new StubSettingsService(new AppSettings
        {
            GitHubProfile = "Yoursel",
            GitHubToken = "secret",
            ActiveGitHubAccountId = Guid.Empty,
            GitHubAccounts =
            [
                new GitHubAccountSettings
                {
                    Id = Guid.Empty,
                    Owner = "Yoursel",
                    Token = "secret"
                }
            ]
        });
        var viewModel = CreateViewModel(new StubApiClient(), settingsService);

        Assert.Empty(viewModel.GitHubAccounts);
        Assert.Null(viewModel.SelectedGitHubAccount);
        Assert.False(viewModel.HasRegisteredGitHubAccount);
        Assert.False(viewModel.SynchronizeAccountCommand.CanExecute(null));
        Assert.False(viewModel.DeleteAccountCommand.CanExecute(null));
        Assert.True(viewModel.CheckConnectionCommand.CanExecute(null));
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.TestNotificationCommand.CanExecute(null));
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
        public ApiOperationResult<GitHubAccountResponse> AddAccountResult { get; init; } =
            ApiOperationResult<GitHubAccountResponse>.Failed("Not configured");
        public ApiOperationResult<GitHubSyncResponse> SyncResult { get; init; } =
            ApiOperationResult<GitHubSyncResponse>.Failed("Not configured");

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

        public Task<ApiOperationResult<GitHubAccountResponse>> AddGitHubAccountAsync(
            string apiUrl, string gitHubProfile, string gitHubToken, CancellationToken ct = default) =>
            Task.FromResult(AddAccountResult);

        public Task<ApiOperationResult<GitHubSyncResponse>> SynchronizeGitHubAccountAsync(
            string apiUrl, Guid accountId, string gitHubToken, bool fullHistory = true, CancellationToken ct = default) =>
            Task.FromResult(SyncResult);

        public Task<ApiOperationResult<bool>> DeleteGitHubAccountAsync(
            string apiUrl, Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<bool>.Failed("Not configured"));

        public Task<ApiOperationResult<MetricsSummaryResponse>> GetMetricsAsync(CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<MetricsSummaryResponse>.Failed("Not configured"));

        public Task<ApiOperationResult<IReadOnlyList<RepositoryResponse>>> GetRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<IReadOnlyList<RepositoryResponse>>.Failed("Not configured"));

        public Task<ApiOperationResult<IReadOnlyList<WorkflowResponse>>> GetWorkflowsAsync(
            Guid repositoryId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<IReadOnlyList<WorkflowResponse>>.Failed("Not configured"));

        public Task<ApiOperationResult<AnalyticsResponse>> GetAnalyticsAsync(
            DateTimeOffset periodStart, DateTimeOffset periodEnd, Guid? repositoryId = null,
            Guid? workflowId = null, PipelineStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<AnalyticsResponse>.Failed("Not configured"));

        public Task<ApiOperationResult<RunDetailsResponse>> GetRunDetailsAsync(
            Guid runId, CancellationToken ct = default) =>
            Task.FromResult(ApiOperationResult<RunDetailsResponse>.Failed("Not configured"));

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

    private sealed class StubSettingsService(AppSettings? initialSettings = null) : IAppSettingsService
    {
        public AppSettings? SavedSettings { get; private set; }

        public AppSettings Load() => initialSettings ?? new AppSettings();

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
