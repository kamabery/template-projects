using Microsoft.AspNetCore.Mvc;

namespace template.api.Controllers;

/// <summary>
/// Development-only relay that hands a browser client the OTLP settings the AppHost gave this API.
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
    /// <remarks>
    /// OTEL_RESOURCE_ATTRIBUTES is deliberately not relayed: it carries this API's own
    /// service.instance.id, and a client that adopted it would file its telemetry under the API's
    /// resource in the dashboard rather than its own.
    /// </remarks>
    public record ClientTelemetryConfig(string? Endpoint, string? Protocol, string? Headers);

    [HttpGet("config", Name = "GetClientTelemetryConfig")]
    [EndpointSummary("OTLP settings for a browser client, so it can export telemetry to the Aspire dashboard")]
    [ProducesResponseType<ClientTelemetryConfig>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClientTelemetryConfig> GetConfig()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        // Null all the way through when the API is running standalone, which is what leaves the
        // client's telemetry switched off rather than pointed at nothing.
        return Ok(new ClientTelemetryConfig(
            configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
            configuration["OTEL_EXPORTER_OTLP_HEADERS"]));
    }
}
