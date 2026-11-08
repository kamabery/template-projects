using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace template.api.Controllers;

/// <summary>
/// Tells a client whether this API wants signed-in writes, and where to sign in.
/// </summary>
/// <remarks>
/// <para>
/// Both front ends ask this at startup rather than carrying their own copy, for the same reason
/// <see cref="ClientTelemetryController"/> exists: the Blazor WebAssembly host's configuration is a
/// static file, and the React bundle is built once and deployed to every environment. The identity
/// provider differs per environment - Keycloak under the AppHost, Entra External ID in Azure - and
/// only this API is told which, through <c>Auth:*</c>. See <see cref="Startup.AuthConfigurator"/>.
/// </para>
/// <para>
/// Not Development-only, unlike the telemetry relay: nothing here is a secret. A public client's id
/// and its authority are what every browser sees on the sign-in redirect anyway.
/// </para>
/// </remarks>
[ApiController]
[Route("auth")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    /// <param name="Enabled">False when no authority is configured: the client hides sign-in and
    /// sends no token.</param>
    /// <param name="Authority">The OIDC issuer the client redirects to.</param>
    /// <param name="ClientId">The public (SPA) client registered with that issuer.</param>
    /// <param name="Scope">Space-separated scopes to request, including the one that makes the
    /// access token's audience this API.</param>
    public record AuthConfig(bool Enabled, string? Authority, string? ClientId, string? Scope);

    /// <param name="Name">Who the access token says the caller is, for the app to show.</param>
    public record CurrentUser(string Name);

    // JWT bearer maps some inbound claims to the long names, and each provider sends a different set.
    private static readonly string[] NameClaims =
        [ClaimTypes.Name, "name", "preferred_username", ClaimTypes.Email, "email", ClaimTypes.NameIdentifier, "sub"];

    [HttpGet("config", Name = "GetAuthConfig")]
    [EndpointSummary("Where a client signs in, or that it does not need to")]
    [ProducesResponseType<AuthConfig>(StatusCodes.Status200OK)]
    public ActionResult<AuthConfig> GetConfig()
    {
        var authority = configuration["Auth:Authority"];

        return string.IsNullOrWhiteSpace(authority)
            ? Ok(new AuthConfig(false, null, null, null))
            : Ok(new AuthConfig(true, authority, configuration["Auth:ClientId"], configuration["Auth:Scope"]));
    }

    // Checked here, not with [Authorize]: with sign-in off no scheme is registered for it to challenge.
    [HttpGet("/me", Name = "GetCurrentUser")]
    [EndpointSummary("Who the signed-in caller is")]
    [ProducesResponseType<CurrentUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUser> GetMe()
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();

        var name = NameClaims.Select(User.FindFirstValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return Ok(new CurrentUser(name ?? "signed in"));
    }
}
