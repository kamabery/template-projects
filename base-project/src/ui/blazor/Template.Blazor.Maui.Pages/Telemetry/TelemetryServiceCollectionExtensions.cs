using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// Client-side RUM for both Blazor hosts: traces, metrics and logs exported to the Aspire dashboard
/// over OTLP.
/// </summary>
/// <remarks>
/// The third of the shared registration calls, beside <c>AddSharedViewModels</c> and
/// <c>AddSharedProviders</c>, and for the same reason - one implementation both hosts call is what
/// stops the WebAssembly and MAUI hosts reporting different things.
/// </remarks>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenTelemetry providers, or does nothing at all when
    /// <see cref="ClientTelemetryOptions.IsEnabled"/> is false. Pair it with
    /// <see cref="ActivateSharedTelemetry"/> on the built application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The providers are built with <c>Sdk.CreateTracerProviderBuilder</c> and its metrics twin
    /// rather than through <c>OpenTelemetry.Extensions.Hosting</c>. That package starts the SDK from
    /// an <c>IHostedService</c>, and a <c>WebAssemblyHost</c> never runs hosted services - the
    /// providers would be configured and then never started, with no error to say so.
    /// </para>
    /// <para>
    /// They are registered as factories rather than built here so their OTLP exporters can take a
    /// client from the application's <c>IHttpClientFactory</c>, which is load-bearing in the
    /// WebAssembly host - see the remarks on <c>ConfigureExporter</c>. Registering them as
    /// singletons also means the container disposes them, which flushes whatever is still buffered
    /// at shutdown.
    /// </para>
    /// <para>
    /// Nothing here is conditional on the host: the differences between MAUI and WebAssembly are
    /// carried as values on <paramref name="options"/>, so this file needs no platform checks and no
    /// preprocessor directives.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSharedTelemetry(this IServiceCollection services, ClientTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Registered whether or not telemetry is on. It is inert without a listening TracerProvider,
        // and MainLayout injects it unconditionally - gating the registration would turn a disabled
        // exporter into a missing-service exception on every standalone run.
        services.AddScoped<NavigationTelemetry>();

        if (!options.IsEnabled)
        {
            return services;
        }

        services.AddHttpClient();

        // AddService last so its service.name wins over anything the relayed attributes carried.
        var resource = ResourceBuilder.CreateDefault()
            .AddAttributes(ClientTelemetryOptions
                .ParseKeyValueList(options.ResourceAttributes)
                .Select(pair => new KeyValuePair<string, object>(pair.Key, pair.Value)))
            .AddService(options.ServiceName);

        services.AddSingleton(serviceProvider => Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(ClientTelemetry.ActivitySource.Name)
            // Puts traceparent on the Refit calls in AddSharedProviders, which is what continues a
            // click in either front end into the API's own spans. The WebAssembly host is genuinely
            // cross-origin, so that header survives preflight only because CorsConfigurator allows
            // any header.
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(exporter =>
                ConfigureExporter(exporter, options, "/v1/traces", serviceProvider, "OtlpTraceExporter"))
            .Build());

        // Metrics only where a MeterProvider can actually export. Its reader collects and exports on
        // a dedicated thread, and the browser runtime never runs that thread - nor, as it turns out,
        // any timer continuation this library could drive it from instead. Registering it there
        // would be wiring that looks live and is not, so the WebAssembly host reports traces and
        // logs and says so.
        if (!options.IsSingleThreadedRuntime)
        {
            services.AddSingleton(serviceProvider => Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resource)
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter =>
                    ConfigureExporter(exporter, options, "/v1/metrics", serviceProvider, "OtlpMetricExporter"))
                .Build());
        }

        // Every ILogger call in this library and in the host is exported from here. That covers the
        // unhandled-exception case for free: both Blazor renderers log a component's unhandled
        // exception through ILogger, which is what the React front end has to patch console.error to
        // achieve.
        services.AddLogging(logging => logging.AddOpenTelemetry(logs =>
        {
            logs.SetResourceBuilder(resource);
            logs.IncludeFormattedMessage = true;
            logs.IncludeScopes = true;
            logs.AddOtlpExporter(exporter => ConfigureExporter(exporter, options, "/v1/logs"));
        }));

        return services;
    }

    /// <summary>
    /// Starts the telemetry providers. Both hosts call this on the built application.
    /// </summary>
    /// <remarks>
    /// Resolving a factory registration is what runs it, and nothing else asks for these - there is
    /// no hosted service to do it, because a WebAssemblyHost does not run them.
    /// </remarks>
    public static IServiceProvider ActivateSharedTelemetry(this IServiceProvider services)
    {
        services.GetService<TracerProvider>();
        services.GetService<MeterProvider>();

        return services;
    }

    /// <summary>
    /// Points one exporter at the dashboard, taking its client from the application's factory.
    /// </summary>
    /// <remarks>
    /// The exporter's own default client is what the WebAssembly host cannot send with, and the
    /// failure is invisible: the SDK reports it on an EventSource, which the browser runtime does
    /// not surface, so the symptom is spans that reach the exporter and are never sent. The log
    /// exporter never had the problem, because <c>AddLogging</c> composes into the application's own
    /// service collection, where the factory already lives.
    /// </remarks>
    private static void ConfigureExporter(
        OtlpExporterOptions exporter,
        ClientTelemetryOptions options,
        string signalPath,
        IServiceProvider serviceProvider,
        string clientName)
    {
        ConfigureExporter(exporter, options, signalPath);

        exporter.HttpClientFactory = () =>
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
    }

    /// <remarks>
    /// The signal path is appended by hand. The SDK only appends one to its own default endpoint -
    /// set <c>Endpoint</c> explicitly and it posts every signal to whatever URL it was given. This is
    /// the same join <c>signalUrl</c> performs in src/ui/react/src/lib/telemetry.ts, and it applies
    /// to HTTP/protobuf only: gRPC multiplexes all three signals over the base endpoint.
    /// </remarks>
    private static void ConfigureExporter(OtlpExporterOptions exporter, ClientTelemetryOptions options, string signalPath)
    {
        exporter.Protocol = options.ExportProtocol;
        exporter.ExportProcessorType = options.IsSingleThreadedRuntime
            ? ExportProcessorType.Simple
            : ExportProcessorType.Batch;
        exporter.Endpoint = exporter.Protocol == OtlpExportProtocol.HttpProtobuf
            ? new Uri($"{options.Endpoint!.TrimEnd('/')}{signalPath}")
            : new Uri(options.Endpoint!);

        if (!string.IsNullOrWhiteSpace(options.Headers))
        {
            // Passed through verbatim: the SDK parses the same key=value,key=value form the AppHost
            // supplied it in.
            exporter.Headers = options.Headers;
        }
    }
}
