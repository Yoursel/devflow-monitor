using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DevFlowMonitor.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DevFlowMonitor.Api.Security;

internal sealed class LocalApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ILocalApiKeyProvider apiKeyProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(LocalApiAuthentication.HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedKey = Encoding.UTF8.GetBytes(values.ToString());
        var expectedKey = Encoding.UTF8.GetBytes(apiKeyProvider.GetApiKey());
        if (providedKey.Length != expectedKey.Length
            || !CryptographicOperations.FixedTimeEquals(providedKey, expectedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid local API key"));
        }

        Claim[] claims = [new(ClaimTypes.NameIdentifier, Environment.UserName)];
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, LocalApiAuthentication.Scheme));
        var ticket = new AuthenticationTicket(principal, LocalApiAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
