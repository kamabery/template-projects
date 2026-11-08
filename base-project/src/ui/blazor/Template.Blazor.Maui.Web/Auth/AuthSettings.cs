using System.Net.Http.Json;

namespace Template.Blazor.Maui.Web.Auth;

/// <summary>
/// What the API says about signing in (<c>GET /auth/config</c>).
/// </summary>
/// <remarks>
/// Fetched at startup, like ClientTelemetryOptions: this host's wwwroot/appsettings.json is one
/// static file for every environment, and the identity provider differs per environment. The shape
/// duplicates the API's AuthController.AuthConfig rather than sharing it, for the same one-way
/// dependency reason the telemetry relay gives.
/// </remarks>
public sealed record AuthSettings(bool Enabled, string? Authority, string? ClientId, string? Scope)
{
    private static readonly AuthSettings Off = new(false, null, null, null);

    /// <summary>Never throws: an API that cannot be reached leaves sign-in off and reads working.</summary>
    public static async Task<AuthSettings> FetchAsync(Uri apiBaseAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = apiBaseAddress };

            var settings = await client.GetFromJsonAsync<AuthSettings>("auth/config", cancellationToken).ConfigureAwait(false);

            return settings is { Enabled: true, Authority: not null, ClientId: not null } ? settings : Off;
        }
        catch (Exception)
        {
            return Off;
        }
    }
}
