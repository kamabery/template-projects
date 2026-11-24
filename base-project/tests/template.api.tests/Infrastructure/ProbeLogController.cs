using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace template.api.tests.Infrastructure;

/// <summary>
/// Logs one Information line inside a real request, so a test can look at what a request adds to a
/// record. It exists only in this test assembly; <see cref="ObservedApi"/> adds it as an
/// application part.
/// </summary>
[ApiController]
[Route("probe-log")]
public sealed class ProbeLogController(ILogger<ProbeLogController> logger) : ControllerBase
{
    [HttpGet("{marker}")]
    public IActionResult Log(string marker)
    {
        logger.LogInformation("Probe {Marker}", marker);
        return Ok();
    }
}
