using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Makes Serilog the logging provider for every service that calls <c>AddServiceDefaults</c>.
/// </summary>
/// <remarks>
/// A sibling of Extensions.cs rather than an edit to it: that file is the stock Aspire one and is
/// meant to be extended, not rewritten.
/// </remarks>
public static class SerilogExtensions
{
    // The standard OTLP exporter variables. The AppHost injects all three into every resource it
    // starts; outside it none are set, which is what switches the OTLP sink off.
    private const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ProtocolVariable = "OTEL_EXPORTER_OTLP_PROTOCOL";
    private const string HeadersVariable = "OTEL_EXPORTER_OTLP_HEADERS";

    /// <summary>
    /// Replaces the logging pipeline with Serilog, writing to the console and - under the AppHost -
    /// to the dashboard over OTLP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this <em>after</em> <c>ConfigureOpenTelemetry</c>, so the <c>ClearProviders</c> below
    /// removes the <c>Logging.AddOpenTelemetry</c> provider that method registers rather than
    /// leaving it behind. It is dead either way: <c>AddSerilog</c> replaces
    /// <see cref="ILoggerFactory"/> with Serilog's own, and its <c>writeToProviders</c> parameter
    /// defaults to false, so records never reach an <see cref="ILoggerProvider"/> at all - which is
    /// what stops every record arriving at the dashboard twice, once through that provider and once
    /// through the OTLP sink configured here. Clearing makes the removal explicit instead of leaving
    /// wiring that looks live and is not.
    /// </para>
    /// <para>
    /// <c>Services.AddSerilog</c> rather than <c>Host.UseSerilog</c>: <typeparamref name="TBuilder"/>
    /// is an <see cref="IHostApplicationBuilder"/> and has no <c>IHostBuilder</c> to configure. The
    /// overload used here also owns the logger and disposes it with the container, which is what
    /// flushes buffered OTLP records at shutdown.
    /// </para>
    /// </remarks>
    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog(logger =>
        {
            logger
                // Levels come from the "Serilog" section of appsettings.json. Serilog does not read
                // "Logging:LogLevel", which is why those sections were replaced rather than left in
                // place to rot. A missing section is a no-op, not an error.
                .ReadFrom.Configuration(builder.Configuration)
                // ConfigureOpenTelemetry asked for IncludeScopes. Serilog carries ILogger scopes on
                // the log context, so this is what keeps scope properties on the records.
                .Enrich.FromLogContext()
                .WriteTo.Console();

            var endpoint = builder.Configuration[EndpointVariable];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            logger.WriteTo.OpenTelemetry(options =>
            {
                // The sink appends /v1/logs itself when the protocol is HttpProtobuf, so this stays
                // the base endpoint exactly as the AppHost supplied it.
                options.Endpoint = endpoint;
                options.Protocol = ReadProtocol(builder.Configuration);
                options.ResourceAttributes["service.name"] = builder.Environment.ApplicationName;

                // Carries the dashboard's per-run x-otlp-api-key.
                foreach (var (key, value) in ParseKeyValueList(builder.Configuration[HeadersVariable]))
                {
                    options.Headers[key] = value;
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Reads <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>, defaulting to gRPC.
    /// </summary>
    /// <remarks>
    /// gRPC is what the .NET OpenTelemetry SDK uses when the variable is absent, so honouring the
    /// variable rather than pinning a protocol keeps this sink pointed at the same endpoint the rest
    /// of the exporters in this project are using - including in a scaffold whose AppHost does not
    /// pin one.
    /// </remarks>
    private static OtlpProtocol ReadProtocol(IConfiguration configuration) =>
        configuration[ProtocolVariable] switch
        {
            "http/protobuf" => OtlpProtocol.HttpProtobuf,
            _ => OtlpProtocol.Grpc
        };

    /// <summary>
    /// Parses the W3C Baggage shape the OTLP environment variables use -
    /// <c>key1=value1,key2=value2</c>, with percent-encoded values.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> ParseKeyValueList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            yield return new KeyValuePair<string, string>(
                pair[..separator].Trim(),
                Uri.UnescapeDataString(pair[(separator + 1)..].Trim()));
        }
    }
}
