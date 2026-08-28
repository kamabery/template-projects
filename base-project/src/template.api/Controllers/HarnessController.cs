using Microsoft.AspNetCore.Mvc;
using template.api.Services;

namespace template.api.Controllers;

/// <summary>
/// Development-only endpoints that force a client's loading spinner and error banner on demand.
/// </summary>
/// <remarks>
/// Every action returns 404 outside Development. Gating here rather than by conditionally
/// registering the controller keeps <c>AddControllers</c>/<c>MapControllers</c> a single
/// unconditional pair, which is the whole registration story for this API.
/// </remarks>
[ApiController]
[Route("harness")]
public sealed class HarnessController(HarnessFaultInjector faults, IHostEnvironment environment) : ControllerBase
{
    public record LatencyRequest(int Milliseconds);

    public record FailNextRequest(int Count);

    [HttpPost("latency", Name = "SetHarnessLatency")]
    [EndpointSummary("Delay every /WeatherForecast response by the given number of milliseconds")]
    public IActionResult SetLatency([FromBody] LatencyRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        faults.SetLatency(request.Milliseconds);

        return Ok(new { faults.LatencyMilliseconds });
    }

    [HttpPost("fail-next", Name = "FailNextHarnessRequests")]
    [EndpointSummary("Make the next N /WeatherForecast requests fail with a 500")]
    public IActionResult FailNext([FromBody] FailNextRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        faults.FailNext(request.Count);

        return Ok(new { faults.FailuresRemaining });
    }

    [HttpPost("reset", Name = "ResetHarness")]
    [EndpointSummary("Clear injected latency and injected failures")]
    public IActionResult Reset()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        // Faults only. The forecast now lives in the database and reset deliberately does not
        // touch it: wiping a store the developer may have edited by hand is a surprise, and the
        // seed is restored on the next startup anyway. Drop the data volume to start clean.
        faults.Reset();

        return Ok();
    }
}
