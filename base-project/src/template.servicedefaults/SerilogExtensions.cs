using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Makes Serilog the front door for logging in every service that calls <c>AddServiceDefaults</c>.
/// </summary>
/// <remarks>
/// A sibling of Extensions.cs rather than an edit to it: that file is the stock Aspire one and is
/// meant to be extended, not rewritten.
/// </remarks>
public static class SerilogExtensions
{
    /// <summary>
    /// Replaces the logger factory with Serilog, writing to the console and forwarding every event to
    /// the OpenTelemetry logging provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One way out: the OpenTelemetry provider <c>ConfigureOpenTelemetry</c> registers is the only
    /// <see cref="ILoggerProvider"/>, and its exporters decide where a record goes - the dashboard
    /// under the AppHost, Application Insights when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is
    /// set, both, or nowhere. <c>AddSerilog</c> replaces <see cref="ILoggerFactory"/> and does not
    /// write to providers itself; <see cref="OpenTelemetryLogForwarder"/> does, with the event's
    /// properties intact. <c>LoggingConfigurationTests</c> in the API suite asserts all of it.
    /// </para>
    /// <para>
    /// Call this <em>before</em> <c>ConfigureOpenTelemetry</c>. <c>ClearProviders</c> below removes
    /// the host's default Console, Debug and EventSource providers - otherwise the forwarder would
    /// write every event to them as well - and must not remove the OpenTelemetry provider, which is
    /// registered after it.
    /// </para>
    /// <para>
    /// In Production the console keeps Warning and above only. The Container Apps environment copies
    /// console output into the same Log Analytics workspace Application Insights writes to, so an
    /// Information line on both would be stored twice. Elsewhere the console shows what the levels
    /// in the "Serilog" section allow. Its format follows the same switch as the exporters: compact
    /// JSON when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set, text otherwise.
    /// </para>
    /// <para>
    /// <c>Services.AddSerilog</c> rather than <c>Host.UseSerilog</c>: <typeparamref name="TBuilder"/>
    /// is an <see cref="IHostApplicationBuilder"/> and has no <c>IHostBuilder</c> to configure. The
    /// overload used here also owns the logger and disposes it with the container.
    /// </para>
    /// </remarks>
    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var consoleLevel = builder.Environment.IsProduction() ? LogEventLevel.Warning : LogEventLevel.Verbose;
        var inAzure = !string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);

        builder.Logging.ClearProviders();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                // Levels come from the "Serilog" section of appsettings.json. Serilog does not read
                // "Logging:LogLevel", which is why those sections were replaced rather than left in
                // place to rot. A missing section is a no-op, not an error.
                .ReadFrom.Configuration(builder.Configuration)
                // Carries ILogger scopes and LogContext properties on the records - entity.* among them.
                .Enrich.FromLogContext()
                // trace_id, span_id, trace_flags, parent_span_id; enduser.id inside a signed-in request.
                .Enrich.With(new TraceContextEnricher())
                .Enrich.With(new EnduserEnricher(services.GetRequiredService<IHttpContextAccessor>()));

            // In Azure the console is compact JSON (CLEF), so the Container Apps console logs keep
            // their structure in Log Analytics; anywhere else it stays readable text.
            if (inAzure)
            {
                logger.WriteTo.Console(new CompactJsonFormatter(), restrictedToMinimumLevel: consoleLevel);
            }
            else
            {
                logger.WriteTo.Console(restrictedToMinimumLevel: consoleLevel);
            }

            logger.WriteTo.Sink(new OpenTelemetryLogForwarder(services.GetServices<ILoggerProvider>()));
        });

        return builder;
    }
}
