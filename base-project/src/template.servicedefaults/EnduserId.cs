using System.Security.Claims;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Who acted, as <c>enduser.id</c>: the token's object id, else its subject, and nothing when the
/// caller is anonymous.
/// </summary>
/// <remarks>
/// The API's auth is generic OIDC. Entra puts a stable object id in <c>oid</c>, which ASP.NET maps to
/// the long objectidentifier claim type unless inbound mapping is off, so both forms are read; other
/// issuers only have <c>sub</c>, which is mapped to <see cref="ClaimTypes.NameIdentifier"/> the same
/// way. Nothing else about the user - name, email, UPN - is ever read here, so none of it can reach a
/// log.
/// </remarks>
internal static class EnduserId
{
    public const string Key = "enduser.id";

    private static readonly string[] ClaimOrder =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "sub",
        ClaimTypes.NameIdentifier,
    ];

    public static string? From(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return ClaimOrder.Select(type => user.FindFirst(type)?.Value).FirstOrDefault(value => !string.IsNullOrEmpty(value));
    }
}
