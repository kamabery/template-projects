using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using template.api.Startup;
using template.api.Telemetry;

namespace template.api.Controllers;

/// <summary>
/// Where a browser posts its OTLP/HTTP JSON traces and logs in Azure; see
/// <see cref="BrowserTelemetryRelay"/>. Found at run time through <c>GET /client-telemetry/config</c>.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous, with or without the auth add-on, so a signed-out page still reports its errors. What
/// keeps it a telemetry path rather than an open write into Application Insights: it exists only
/// when Application Insights is configured, takes traces and logs only (no metrics route), JSON only,
/// 64 KB at most, from this app's own pages (<see cref="BrowserOriginGuard"/>), at
/// <see cref="OtlpRelayConfigurator.PermitsPerMinute"/> requests a minute per client.
/// </para>
/// <para>
/// "otel", not "api/otel": UsePathBase("/api") has removed the prefix by the time routing runs.
/// Left out of the OpenAPI contract - no generated client calls it; the OTel exporters do.
/// </para>
/// </remarks>
[ApiController]
[Route("otel/v1")]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting(OtlpRelayConfigurator.RateLimitPolicy)]
[Consumes("application/json")]
public sealed class OtlpRelayController(BrowserTelemetryRelay relay, IConfiguration configuration) : ControllerBase
{
    public const int MaxBodyBytes = 64 * 1024;

    [HttpPost("traces")]
    public Task<IActionResult> Traces(CancellationToken cancellationToken) =>
        RelayAsync(relay.RelaySpans, cancellationToken);

    [HttpPost("logs")]
    public Task<IActionResult> Logs(CancellationToken cancellationToken) =>
        RelayAsync(relay.RelayLogs, cancellationToken);

    private async Task<IActionResult> RelayAsync(Action<JsonElement> relayTo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            return NotFound();
        }

        if (!BrowserOriginGuard.IsAllowed(Request, CorsConfigurator.ReadAllowedOrigins(configuration)))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (Request.ContentLength > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // Read with the cap, not trusting Content-Length: a chunked body has none.
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (body.Length + read > MaxBodyBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            body.Write(buffer, 0, read);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body.ToArray());
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        using (document)
        {
            relayTo(document.RootElement);
        }

        // An empty ExportServiceResponse: everything was accepted.
        return Content("{}", "application/json");
    }
}
