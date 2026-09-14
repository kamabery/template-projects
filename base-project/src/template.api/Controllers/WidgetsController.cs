using Microsoft.AspNetCore.Mvc;
using template.data;
using template.data.Repositories;
using template.api.Services;

namespace template.api.Controllers;

/// <summary>
/// The sample resource's endpoints. This is the file a scaffolded project edits first.
/// </summary>
/// <remarks>
/// The attributes are not decoration: <c>[Route("[controller]")]</c> puts this at /Widgets, the
/// <c>Name</c> on <see cref="Get"/> becomes the OpenAPI operationId, and
/// <c>[ProducesResponseType&lt;T&gt;]</c> is what puts a schema rather than a bare 200 in the
/// document. Both front ends' types are generated from that document, so an endpoint that skips
/// them produces a client with no types. See docs/ADDING-A-RESOURCE.md.
/// </remarks>
[ApiController]
[Route("[controller]")]
public class WidgetsController(
    IRepository<Widget> widgets,
    HarnessFaultInjector faults,
    ILogger<WidgetsController> logger) : ControllerBase
{
    [HttpGet(Name = "GetWidgets")]
    [EndpointSummary("List every widget")]
    [ProducesResponseType<IEnumerable<Widget>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Widget>>> Get(CancellationToken cancellationToken)
    {
        // The injected-fault check is how both front ends' error banners and spinners get
        // exercised without unplugging the network. Inert unless /harness/* has armed it. It runs
        // before the read on purpose: an armed fault should not depend on the database being up.
        if (await faults.ShouldFailAsync(cancellationToken))
        {
            logger.LogWarning("Failing GET /Widgets because a fault was injected.");

            return Problem(
                detail: "Injected failure from the test harness.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Read, not generate. The rows are seeded by WidgetSeeder at startup, so this returns the
        // same set every time until the seed goes stale.
        return Ok(await widgets.GetAllAsync(cancellationToken));
    }
}
