namespace DevFlowMonitor.Api.GitHub;

internal static class GitHubOwnerParser
{
    public static string Parse(string profileOrOwner)
    {
        if (string.IsNullOrWhiteSpace(profileOrOwner))
            return string.Empty;

        var value = profileOrOwner.Trim().TrimStart('@').TrimEnd('/');

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return IsGitHubHost(uri.Host) ? GetFirstPathSegment(uri.AbsolutePath) : string.Empty;

        if (value.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            value = value["github.com/".Length..];
        else if (value.StartsWith("www.github.com/", StringComparison.OrdinalIgnoreCase))
            value = value["www.github.com/".Length..];

        return GetFirstPathSegment(value);
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);

    private static string GetFirstPathSegment(string value) =>
        value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}
