using Microsoft.AspNetCore.Mvc;
using template.data;
using template.data.Repositories;
using template.api.Services;
using template.api.Telemetry;

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
/// <c>[EntityType]</c> names what these actions log about: <see cref="EntityScopeFilter"/> puts it and
/// the route's <c>{id}</c> on every record, and the lines themselves are in <see cref="WidgetLog"/>.
/// </remarks>
[ApiController]
[Route("[controller]")]
[EntityType(typeof(Widget))]
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
            logger.FaultInjected(Request.Path);

            return Problem(
                detail: "Injected failure from the test harness.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Read, not generate. The rows are seeded by WidgetSeeder at startup, so this returns the
        // same set every time until the seed goes stale.
        var all = await widgets.GetAllAsync(cancellationToken);

        // A list has no single id, so the scope names the type only.
        using (logger.ForEntity(nameof(Widget)))
        {
            logger.WidgetsListed(all.Count);
        }

        return Ok(all);
    }

    /// <remarks>
    /// The entity scope comes from the <c>{id}</c> in the route (see <see cref="EntityScopeFilter"/>),
    /// so a miss is logged under <c>entity.id</c> without naming it here. Found is a read, at Debug;
    /// not found is at Information, and it is what the factory's telemetry check asks for with id 0.
    /// </remarks>
    [HttpGet("{id:int}", Name = "GetWidget")]
    [EndpointSummary("Get one widget by its id")]
    [ProducesResponseType<Widget>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<Widget>> GetWidget(int id, CancellationToken cancellationToken)
    {
        var widget = await widgets.GetByIdAsync(id, cancellationToken);

        if (widget is null)
        {
            logger.WidgetNotFound();
            return Problem(detail: $"No widget has id {id}.", statusCode: StatusCodes.Status404NotFound);
        }

        logger.WidgetFound();
        return Ok(widget);
    }
}
