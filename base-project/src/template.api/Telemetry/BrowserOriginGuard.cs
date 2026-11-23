namespace template.api.Telemetry;

/// <summary>
/// Whether a request to the relay came from one of this app's own pages.
/// </summary>
/// <remarks>
/// <para>
/// <c>Sec-Fetch-Site</c>, not Origin against Host: behind the React Static Web App's linked backend
/// this API sees the container app's host, not the page's, so a same-origin post would look
/// cross-origin. The browser sets <c>Sec-Fetch-Site</c> itself and a page cannot override it.
/// </para>
/// <para>
/// The Blazor WebAssembly host is cross-origin by design, on its own Static Web App, so an Origin
/// in <c>Cors:AllowedOrigins</c> is accepted too - the same list that lets it call the API at all.
/// A request with neither, such as curl, is refused; anyone can forge both, which is why the
/// relay is also capped and rate limited.
/// </para>
/// </remarks>
public static class BrowserOriginGuard
{
    public static bool IsAllowed(HttpRequest request, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.Equals(request.Headers["Sec-Fetch-Site"], "same-origin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var origin = request.Headers.Origin.ToString();
        return origin.Length > 0 && allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
