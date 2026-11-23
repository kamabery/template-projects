using Microsoft.AspNetCore.Mvc;

namespace template.api.Controllers;

/// <summary>
/// Tells a browser client where its telemetry goes: in Development, the OTLP settings the AppHost
/// gave this API; in Azure, the path of this API's own relay (<see cref="OtlpRelayController"/>).
/// </summary>
/// <remarks>
/// <para>
/// A browser cannot read the AppHost's environment, and the Blazor WebAssembly host's
/// <c>wwwroot/appsettings.json</c> is a static file served as-is - so unlike the React app, which
/// gets <c>OTEL_*</c> injected into the Vite process and forwarded into its bundle, the WASM host has
/// no build step to inject anything into. The dashboard's OTLP API key is also generated fresh on
/// every run, so it cannot be committed. This API already receives all of it, so it passes it on.
/// </para>
/// <para>
/// Deliberately not wrapped in a scaffolding conditional. <c>templating/stage.ps1</c> strips
/// <c>includeBlazorWeb</c> as statically false for aspire-base, so a conditional region here would be
/// deleted from every base scaffold - including one that adds the Blazor UI afterwards. It is
/// Development-only and useful to any browser client, so it ships unconditionally.
/// </para>
/// <para>
/// The response shape is duplicated by <c>ClientTelemetryOptions</c> in Template.Blazor.Maui.Pages
/// rather than shared: the dependency between the API and that library runs one way only, and it
/// does not run this way.
/// </para>
/// </remarks>
[ApiController]
[Route("client-telemetry")]
public sealed class ClientTelemetryController(IConfiguration configuration, IHostEnvironment environment) : ControllerBase
{
    /// <param name="Endpoint">The OTLP base endpoint, or null when nothing is exporting.</param>
    /// <param name="Protocol">The OTEL_EXPORTER_OTLP_PROTOCOL value. A browser can only use
    /// <c>http/protobuf</c>; the client checks this rather than assuming.</param>
    /// <param name="Headers">W3C Baggage form, carrying the dashboard's x-otlp-api-key.</param>
    /// <param name="Relay">The browser telemetry relay's path, relative to this API's base address
    /// (<c>otel</c>, so <c>otel/v1/traces</c> and <c>otel/v1/logs</c>), or null when Application
    /// Insights is not configured. A client uses it when there is no Endpoint.</param>
    /// <remarks>
    /// OTEL_RESOURCE_ATTRIBUTES is deliberately not relayed: it carries this API's own
    /// service.instance.id, and a client that adopted it would file its telemetry under the API's
    /// resource in the dashboard rather than its own.
    /// </remarks>
    public record ClientTelemetryConfig(string? Endpoint, string? Protocol, string? Headers, string? Relay);

    [HttpGet("config", Name = "GetClientTelemetryConfig")]
    [EndpointSummary("Where a browser client sends its telemetry: the Aspire dashboard in Development, the relay in Azure")]
    [ProducesResponseType<ClientTelemetryConfig>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClientTelemetryConfig> GetConfig()
    {
        // Run time, not build time: a factory preview gets its connection string when it is
        // deployed, so no build variable could say whether the relay is there.
        var relay = string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])
            ? null
            : RelayPath;

        if (!environment.IsDevelopment())
        {
            // Outside Development only the relay is named: the OTLP settings carry the dashboard's
            // key and must not leave a development machine.
            return relay is null ? NotFound() : Ok(new ClientTelemetryConfig(null, null, null, relay));
        }

        // Null all the way through when the API is running standalone, which is what leaves the
        // client's telemetry switched off rather than pointed at nothing.
        return Ok(new ClientTelemetryConfig(
            configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
            configuration["OTEL_EXPORTER_OTLP_HEADERS"],
            relay));
    }

    /// <summary>OtlpRelayController's route, relative to the API base.</summary>
    private const string RelayPath = "otel";
}
