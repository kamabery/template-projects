using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace Template.Blazor.Maui.Web.Auth;

/// <summary>
/// Puts the signed-in user's access token on API calls, and sends the call anyway when there is
/// none.
/// </summary>
/// <remarks>
/// Not the framework's AuthorizationMessageHandler: that one throws
/// AccessTokenNotAvailableException for a signed-out user, and reads on this API are anonymous - a
/// visitor who has not signed in must still see the list. The API decides what needs a token.
/// </remarks>
public sealed class OptionalBearerHandler(IAccessTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var result = await tokens.RequestAccessToken();
        if (result.TryGetToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
