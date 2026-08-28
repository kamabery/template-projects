using Marten;
using Microsoft.AspNetCore.Mvc;
using template.events.Domain;
using template.events.Projections;
using Wolverine;

namespace template.events.Controllers;

/// <summary>
/// The HTTP face of the slice: two commands in, one projection out.
/// </summary>
/// <remarks>
/// Controllers rather than Wolverine.HTTP endpoints, to match template.api and keep one HTTP idiom
/// across the workspace. The controller does no domain work at all - it turns a request into a
/// command, hands it to <see cref="IMessageBus"/> and gets out of the way.
/// </remarks>
[ApiController]
[Route("orders")]
public class OrdersController(IMessageBus bus, IQuerySession query) : ControllerBase
{
    [HttpPost(Name = "PlaceOrder")]
    [EndpointSummary("Place an order, starting its event stream")]
    [ProducesResponseType<PlaceOrderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlaceOrderResponse>> Place(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        // InvokeAsync, not SendAsync: this runs the handler inline and waits for its result, so the
        // new id can come back in the response. SendAsync would queue it and return immediately.
        var orderId = await bus.InvokeAsync<Guid>(
            new PlaceOrder(request.Customer, request.Total), cancellationToken);

        return CreatedAtRoute("GetOrder", new { id = orderId }, new PlaceOrderResponse(orderId));
    }

    [HttpPost("{id:guid}/approve", Name = "ApproveOrder")]
    [EndpointSummary("Approve an order; appends to its stream and publishes order-approved-v1")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await bus.InvokeAsync(new ApproveOrder(id), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // Order.EnsureCanBeApproved rejected it - the order is already approved. A conflict
            // rather than a 500: the request was well formed, the aggregate just says no.
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }

        // Accepted, not Ok. The event is committed, but OrderSummaryProjection is async, so a GET
        // issued right now may still report the old status.
        return Accepted();
    }

    [HttpGet("{id:guid}", Name = "GetOrder")]
    [EndpointSummary("Read the order summary projection")]
    [ProducesResponseType<OrderSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        // Reads the projection document, not the stream. A 404 here right after a POST does not
        // mean the order is missing - it means the async daemon has not caught up yet.
        var summary = await query.LoadAsync<OrderSummary>(id, cancellationToken);

        return summary is null ? NotFound() : Ok(summary);
    }
}

public sealed record PlaceOrderRequest(string Customer, decimal Total);

public sealed record PlaceOrderResponse(Guid OrderId);
