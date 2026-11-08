using Microsoft.AspNetCore.Authorization;
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
/// <para>
/// <c>[AllowAnonymous]</c> because these are writes, and AuthConfigurator puts every write behind
/// sign-in once an authority is configured. The harness exists to be driven from the UI in
/// Development; making it sign in first would only get in the way.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("harness")]
public sealed class HarnessController(HarnessFaultInjector faults, IHostEnvironment environment) : ControllerBase
{
    public record LatencyRequest(int Milliseconds);

    public record FailNextRequest(int Count);

    /// <summary>What the harness is currently doing to every response.</summary>
    /// <remarks>
    /// A declared type rather than an anonymous one, and the reason is the contract: an action
    /// returning <c>Ok(new { ... })</c> has no schema in the OpenAPI document, so the clients
    /// generated from that document get an untyped blob for it. See docs/ADDING-A-RESOURCE.md.
    /// </remarks>
    public record HarnessState(int LatencyMilliseconds, int FailuresRemaining);

    [HttpPost("latency", Name = "SetHarnessLatency")]
    [EndpointSummary("Delay every /Widgets response by the given number of milliseconds")]
    [ProducesResponseType<HarnessState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<HarnessState> SetLatency([FromBody] LatencyRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        faults.SetLatency(request.Milliseconds);

        return Ok(CurrentState());
    }

    [HttpPost("fail-next", Name = "FailNextHarnessRequests")]
    [EndpointSummary("Make the next N /Widgets requests fail with a 500")]
    [ProducesResponseType<HarnessState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<HarnessState> FailNext([FromBody] FailNextRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        faults.FailNext(request.Count);

        return Ok(CurrentState());
    }

    [HttpPost("reset", Name = "ResetHarness")]
    [EndpointSummary("Clear injected latency and injected failures")]
    [ProducesResponseType<HarnessState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<HarnessState> Reset()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        // Faults only. The rows live in the database and reset deliberately does not
        // touch it: wiping a store the developer may have edited by hand is a surprise, and the
        // seed is restored on the next startup anyway. Drop the data volume to start clean.
        faults.Reset();

        return Ok(CurrentState());
    }

    private HarnessState CurrentState() => new(faults.LatencyMilliseconds, faults.FailuresRemaining);
}
