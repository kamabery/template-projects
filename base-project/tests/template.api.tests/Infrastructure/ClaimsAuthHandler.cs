using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace template.api.tests.Infrastructure;

/// <summary>
/// Signs in a caller with exactly the claims named in <see cref="ClaimsHeader"/>, as
/// <c>type=value;type=value</c>; a request without the header stays anonymous. Lets a test choose
/// between an Entra-style token (<c>oid</c>) and a generic OIDC one (<c>sub</c> only).
/// </summary>
public sealed class ClaimsAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestClaims";
    public const string ClaimsHeader = "X-Test-Claims";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ClaimsHeader, out var header)) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = header.ToString()
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => new Claim(pair[0], pair[1]));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
