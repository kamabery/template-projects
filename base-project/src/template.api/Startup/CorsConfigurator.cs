namespace template.api.Startup;

/// <summary>
/// CORS exists here for the Blazor WebAssembly host only.
/// </summary>
/// <remarks>
/// Three clients reach this API and only one of them needs a policy. The React app is served by
/// Vite, which proxies <c>/api</c> to this API, so its requests are same-origin. The MAUI host
/// issues its requests from a native <see cref="System.Net.Http.HttpClient"/>, so no Origin header
/// is sent and no preflight happens. The WebAssembly host is a genuinely different origin from
/// this API and its requests are preflighted, so without this policy every one of them fails.
/// </remarks>
public static class CorsConfigurator
{
    public const string WasmClientPolicy = "WasmClient";

    private const string AllowedOriginsKey = "Cors:AllowedOrigins";

    public static IHostApplicationBuilder ConfigureCorsBuilder(this IHostApplicationBuilder builder)
    {
        var allowedOrigins = ReadAllowedOrigins(builder.Configuration);

        builder.Services.AddCors(options => options.AddPolicy(
            WasmClientPolicy,
            policy => policy
                // Named origins rather than AllowAnyOrigin, so the config documents the actual
                // dependency. AllowCredentials is deliberately absent: nothing here uses cookies,
                // and a bearer token only needs the header to survive preflight.
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()));

        return builder;
    }

    /// <summary>
    /// Warns when no origins are configured, because the resulting failure is otherwise silent from
    /// this side.
    /// </summary>
    /// <remarks>
    /// <c>WithOrigins([])</c> allows nothing, and a blocked request still reaches the controller and
    /// still returns 200 - it is only missing the <c>Access-Control-Allow-Origin</c> header, which
    /// the browser enforces on the client. So this API logs a perfectly healthy request while the
    /// WebAssembly host reports "TypeError: Failed to fetch" and the other two clients keep
    /// working. That asymmetry is expensive to diagnose from the client, so say it here at startup.
    /// </remarks>
    public static WebApplication WarnOnMissingCorsOrigins(this WebApplication app)
    {
        if (ReadAllowedOrigins(app.Configuration).Length == 0)
        {
            app.Logger.LogWarning(
                "{Key} is empty, so no browser origin is allowed. Requests from the WebAssembly " +
                "host will fail with \"TypeError: Failed to fetch\" even though this API returns " +
                "200, because the response carries no Access-Control-Allow-Origin header. The " +
                "React and MAUI clients are unaffected. Set {Key} to the WebAssembly host's origin.",
                AllowedOriginsKey,
                AllowedOriginsKey);
        }

        return app;
    }

    private static string[] ReadAllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];
}
