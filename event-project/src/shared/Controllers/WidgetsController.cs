using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using template.events.Abstractions;
using template.events.Domain;
using template.events.Projections;
using Wolverine;

namespace template.events.Controllers;

/// <summary>
/// The HTTP face of the slice: two commands in, one projection out.
/// </summary>
/// <remarks>
/// <para>Controllers rather than Wolverine.HTTP endpoints, to match template.api and keep one HTTP
/// idiom across the workspace. The controller does no domain work at all - it turns a request into
/// a command, hands it to <see cref="IMessageBus"/> and gets out of the way.</para>
/// <para>It lives in the shared project rather than in each host, which is what
/// <see cref="IWidgetSummaries"/> buys: the one store-specific thing it does is load a document.
/// Each host registers its own implementation and adds this assembly as an MVC application part -
/// AddControllers only scans the entry assembly by default.</para>
/// </remarks>
[ApiController]
[Route("widgets")]
public class WidgetsController(IMessageBus bus, IWidgetSummaries summaries) : ControllerBase
{
    [HttpPost(Name = "CreateWidget")]
    [EndpointSummary("Create a widget, starting its event stream")]
    [ProducesResponseType<CreateWidgetResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateWidgetResponse>> Create(
        CreateWidgetRequest request,
        CancellationToken cancellationToken)
    {
        // InvokeAsync, not SendAsync: this runs the handler inline and waits for its result, so the
        // new id can come back in the response. SendAsync would queue it and return immediately.
        var widgetId = await bus.InvokeAsync<Guid>(
            new CreateWidget(request.Name, request.Quantity), cancellationToken);

        return CreatedAtRoute("GetWidget", new { id = widgetId }, new CreateWidgetResponse(widgetId));
    }

    [HttpPost("{id:guid}/complete", Name = "CompleteWidget")]
    [EndpointSummary("Complete a widget; appends to its stream and publishes widget-completed-v1")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await bus.InvokeAsync(new CompleteWidget(id), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // Widget.EnsureCanBeCompleted rejected it - the widget is already completed. A conflict
            // rather than a 500: the request was well formed, the aggregate just says no.
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }

        // Accepted, not Ok. The event is committed, but WidgetSummaryProjection is async, so a GET
        // issued right now may still report the old status.
        return Accepted();
    }

    [HttpGet("{id:guid}", Name = "GetWidget")]
    [EndpointSummary("Read the widget summary projection")]
    [ProducesResponseType<WidgetSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WidgetSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        // Reads the projection document, not the stream. A 404 here right after a POST does not
        // mean the widget is missing - it means the async daemon has not caught up yet.
        var summary = await summaries.LoadAsync(id, cancellationToken);

        return summary is null ? NotFound() : Ok(summary);
    }
}

public sealed record CreateWidgetRequest(string Name, int Quantity);

public sealed record CreateWidgetResponse(Guid WidgetId);
