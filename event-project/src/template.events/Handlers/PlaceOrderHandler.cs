using Marten;
using template.events.Domain;

namespace template.events.Handlers;

/// <summary>
/// Starts a new order stream.
/// </summary>
/// <remarks>
/// No <c>[AggregateHandler]</c> here, unlike <see cref="ApproveOrderHandler"/>: there is no
/// existing aggregate to load and no expected version to enforce, because this call is what brings
/// the stream into being. <c>StartStream</c> fails if the id is already taken, which is the
/// concurrency protection appropriate to a creation.
/// </remarks>
public static class PlaceOrderHandler
{
    public static Guid Handle(PlaceOrder command, IDocumentSession session, TimeProvider clock)
    {
        var orderId = Guid.NewGuid();

        var placed = new OrderPlaced(orderId, command.Customer, command.Total, clock.GetUtcNow());

        session.Events.StartStream<Order>(orderId, placed);

        // No SaveChangesAsync. Policies.AutoApplyTransactions() in Program.cs owns the transaction
        // and commits it around this handler; calling it here would commit early and take the
        // outgoing messages out of the same unit of work.
        return orderId;
    }
}
