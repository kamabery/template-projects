using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// Everything <see cref="TelemetryServiceCollectionExtensions.AddSharedTelemetry"/> needs to point a
/// host's telemetry at the Aspire dashboard.
/// </summary>
/// <remarks>
/// The two hosts obtain these differently because their runtimes differ, not by preference. The MAUI
/// host is a process the AppHost starts, so it reads them straight out of its own environment
/// (<see cref="FromConfiguration"/>). A browser has no environment to read and the WebAssembly host's
/// wwwroot/appsettings.json is a static file that cannot carry the dashboard's per-run API key, so it
/// asks the API for them instead (<see cref="FetchAsync"/>).
/// </remarks>
public sealed record ClientTelemetryOptions
{
    /// <summary>The OTLP base endpoint. Null or blank switches all telemetry off.</summary>
    public string? Endpoint { get; init; }

    /// <summary>The OTEL_EXPORTER_OTLP_PROTOCOL value: <c>grpc</c> or <c>http/protobuf</c>.</summary>
    public string? Protocol { get; init; }

    /// <summary>Headers in W3C Baggage form, carrying the dashboard's x-otlp-api-key.</summary>
    public string? Headers { get; init; }

    /// <summary>Resource attributes in W3C Baggage form. Left null by the WebAssembly host - see
    /// <see cref="FetchAsync"/>.</summary>
    public string? ResourceAttributes { get; init; }

    /// <summary>The name this host appears under in the dashboard.</summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Whether this host runs on a runtime with no usable background threads - true for the
    /// WebAssembly host, false for MAUI.
    /// </summary>
    /// <remarks>
    /// Two things in the SDK assume a thread they can drain a queue on: the batching export processor
    /// and the periodic metric reader. In the browser that thread is created without error and then
    /// never runs, so telemetry is queued and silently never sent. When this is set, spans export one
    /// at a time as they end, and metrics are not registered at all - there is no way to drive that
    /// reader from the browser's event loop either.
    /// </remarks>
    public bool IsSingleThreadedRuntime { get; init; }

    /// <summary>
    /// The API's browser telemetry relay, in Azure, where there is no dashboard: traces and logs go
    /// there as OTLP/HTTP JSON and on to Application Insights. Null when <see cref="Endpoint"/> is
    /// set, or when the API has no relay.
    /// </summary>
    public Uri? RelayEndpoint { get; init; }

    /// <summary>Nothing is exported without somewhere to send it - the same gate the React front end
    /// applies in src/ui/react/src/lib/telemetryTarget.ts and servicedefaults applies in
    /// Extensions.cs.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Endpoint) || RelayEndpoint is not null;

    /// <summary>
    /// gRPC is the .NET OpenTelemetry SDK's default when the protocol variable is absent.
    /// </summary>
    public OtlpExportProtocol ExportProtocol =>
        Protocol == "http/protobuf" ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;

    /// <summary>
    /// Reads the standard OTLP variables out of a host's configuration. Used by the MAUI host, whose
    /// configuration includes the environment the AppHost started it with.
    /// </summary>
    public static ClientTelemetryOptions FromConfiguration(IConfiguration configuration, string serviceName) =>
        new()
        {
            Endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            Protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
            Headers = configuration["OTEL_EXPORTER_OTLP_HEADERS"],
            ResourceAttributes = configuration["OTEL_RESOURCE_ATTRIBUTES"],
            // OTEL_SERVICE_NAME names the AppHost resource, which is what the dashboard files this
            // host's telemetry under; the argument is the fallback for a standalone run.
            ServiceName = configuration["OTEL_SERVICE_NAME"] ?? serviceName
        };

    /// <summary>
    /// Asks the API for the settings, for a host that cannot read an environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns telemetry switched off rather than throwing on any failure: no API yet, an API running
    /// outside Development (the endpoint 404s there), or a scaffold whose API predates it. A front end
    /// must still start when its telemetry cannot.
    /// </para>
    /// <para>
    /// The relayed <c>OTEL_RESOURCE_ATTRIBUTES</c> is deliberately dropped. It carries the API's own
    /// service.instance.id, so copying it would file this host's browser telemetry under the API's
    /// resource in the dashboard. Only the endpoint and its credentials are transferable.
    /// </para>
    /// <para>
    /// A browser cannot speak gRPC, so a non-<c>http/protobuf</c> protocol leaves telemetry off
    /// rather than posting protobuf at a gRPC port and failing silently. That is what the
    /// <c>WithOtlpExporter(OtlpProtocol.HttpProtobuf)</c> call on the api resource in
    /// .aspire/apphost.cs is there to satisfy.
    /// </para>
    /// </remarks>
    public static async Task<ClientTelemetryOptions> FetchAsync(
        Uri apiBaseAddress,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = apiBaseAddress };

        return await FetchAsync(client, serviceName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="FetchAsync(Uri, string, CancellationToken)"/> on a client whose BaseAddress is the
    /// API. A dashboard endpoint wins; otherwise the relay the API names, resolved against that
    /// base address, is used in Azure.
    /// </summary>
    public static async Task<ClientTelemetryOptions> FetchAsync(
        HttpClient api,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(api);

        var disabled = new ClientTelemetryOptions { ServiceName = serviceName };

        try
        {
            var relayed = await api
                .GetFromJsonAsync<RelayedConfig>("client-telemetry/config", cancellationToken)
                .ConfigureAwait(false);

            if (relayed?.Protocol == "http/protobuf" && !string.IsNullOrWhiteSpace(relayed.Endpoint))
            {
                return disabled with
                {
                    Endpoint = relayed.Endpoint,
                    Protocol = relayed.Protocol,
                    Headers = relayed.Headers
                };
            }

            if (!string.IsNullOrWhiteSpace(relayed?.Relay) && api.BaseAddress is { } baseAddress)
            {
                return disabled with { RelayEndpoint = new Uri(baseAddress, relayed.Relay) };
            }

            return disabled;
        }
        catch (Exception)
        {
            return disabled;
        }
    }

    /// <summary>
    /// Parses the W3C Baggage shape the OTLP variables use - <c>key1=value1,key2=value2</c>, with
    /// percent-encoded values. The C# counterpart of <c>parseKeyValueList</c> in
    /// src/ui/react/src/lib/telemetry.ts.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> ParseKeyValueList(string? raw)
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

    /// <summary>
    /// Mirrors template.api's <c>ClientTelemetryController.ClientTelemetryConfig</c>. Declared here
    /// rather than shared because this library must not reference the API - the dependency runs the
    /// other way.
    /// </summary>
    private sealed record RelayedConfig(string? Endpoint, string? Protocol, string? Headers, string? Relay);
}
