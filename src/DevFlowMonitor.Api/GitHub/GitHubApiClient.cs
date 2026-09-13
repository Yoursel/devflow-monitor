using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevFlowMonitor.Api.GitHub;

internal sealed class GitHubApiClient(
    HttpClient httpClient,
    ILogger<GitHubApiClient> logger) : IGitHubApiClient
{
    public async Task<GitHubActionsResult<T>> GetAsync<T>(
        string relativeUrl,
        string token,
        string operationName,
        string failedMessage,
        CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(relativeUrl, token);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{OperationName} request returned HTTP {StatusCode}",
                    operationName,
                    (int)response.StatusCode);

                return GitHubActionsResult<T>.Failed(CreateHttpErrorMessage((int)response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);

            return result is null
                ? GitHubActionsResult<T>.Failed("GitHub вернул пустой ответ")
                : GitHubActionsResult<T>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error while loading {OperationName}", operationName);
            return GitHubActionsResult<T>.Failed(failedMessage);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("{OperationName} request timed out", operationName);
            return GitHubActionsResult<T>.Failed("Превышено время ожидания GitHub");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid {OperationName} response", operationName);
            return GitHubActionsResult<T>.Failed("GitHub вернул ответ в некорректном формате");
        }
    }

    private static HttpRequestMessage CreateRequest(string relativeUrl, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("DevFlowMonitor");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        return request;
    }

    private static string CreateHttpErrorMessage(int statusCode) =>
        statusCode switch
        {
            401 => "GitHub отклонил токен доступа",
            403 => "Нет доступа к GitHub Actions или превышен лимит запросов",
            404 => "GitHub репозиторий не найден или нет доступа",
            _ => $"GitHub вернул HTTP {statusCode}"
        };
}
