using System.Threading.RateLimiting;
using template.api.Telemetry;

namespace template.api.Startup;

/// <summary>
/// The browser telemetry relay at <c>/api/otel</c>: the relay itself and the rate limit on it.
/// </summary>
/// <remarks>
/// The limit is per client, keyed on the first <c>X-Forwarded-For</c> entry: behind the Static
/// Web App and the Container Apps ingress the connection's address is a proxy, so it would put every
/// browser in one bucket. The header is read here and nowhere else - no forwarded-headers middleware
/// for the whole app - and it can be spoofed, which is acceptable for a rate limit.
/// </remarks>
public static class OtlpRelayConfigurator
{
    public const string RateLimitPolicy = "otel-relay";

    /// <summary>Requests per client per minute. A browser's batching exporters post a few times a
    /// minute; the WebAssembly host posts each span as it ends, so this leaves it room.</summary>
    public const int PermitsPerMinute = 120;

    public static IHostApplicationBuilder ConfigureOtlpRelayBuilder(this IHostApplicationBuilder builder)
    {
        // The source servicedefaults already traces (AddSource(ApplicationName)), so relayed spans
        // leave by the same exporters as the API's own.
        builder.Services.AddSingleton(services => new BrowserTelemetryRelay(
            builder.Environment.ApplicationName,
            services.GetRequiredService<ILoggerFactory>()));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context.Request),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

        return builder;
    }

    private static string ClientKey(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-For"].ToString();
        var first = forwarded.Split(',', 2)[0].Trim();

        return first.Length > 0
            ? first
            : request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
