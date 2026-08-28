using Marten.Events.Aggregation;
using template.events.Domain;

namespace template.events.Projections;

/// <summary>
/// Builds <see cref="OrderSummary"/> from one order's stream.
/// </summary>
/// <remarks>
/// <para>Registered as <c>ProjectionLifecycle.Async</c> in Program.cs, so the async daemon applies
/// these events on a background thread with its own checkpoint. That is what makes the read model
/// independently rebuildable and gives it a lag metric, and it is what the spec prefers over
/// inline.</para>
/// <para>The consequence is eventual consistency: a GET issued immediately after the POST that
/// approved an order can still see the old status, or nothing at all for a just-placed order. That
/// is correct behaviour, not a race to fix. If a scaffolded project needs read-your-writes here
/// instead, change the lifecycle to <c>ProjectionLifecycle.Inline</c> in Program.cs and drop the
/// AddAsyncDaemon call - at the cost of doing the projection work inside the write transaction.
/// </para>
/// <para>Every method is deterministic and depends only on its event, which is what makes a rebuild
/// from the stream produce the same document as the original run.</para>
/// </remarks>
public sealed class OrderSummaryProjection : SingleStreamProjection<OrderSummary, Guid>
{
    public static OrderSummary Create(OrderPlaced placed) => new()
    {
        Id = placed.OrderId,
        Customer = placed.Customer,
        Total = placed.Total,
        Status = OrderStatus.Placed,
        PlacedAtUtc = placed.OccurredAtUtc
    };

    public static void Apply(OrderApproved approved, OrderSummary summary)
    {
        summary.Status = OrderStatus.Approved;
        summary.ApprovedAtUtc = approved.OccurredAtUtc;
    }
}
