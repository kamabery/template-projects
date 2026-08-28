using template.events.Contracts;
using template.events.Domain;
using Wolverine;
using Wolverine.Marten;

namespace template.events.Handlers;

/// <summary>
/// The spec's worked example: load an aggregate, check its invariants, decide what happened.
/// </summary>
/// <remarks>
/// <para><c>[AggregateHandler]</c> generates the surrounding workflow. It reads the stream id from
/// the command's <c>OrderId</c> by convention, calls <c>FetchForWriting&lt;Order&gt;</c> to
/// live-aggregate the current state, invokes this method, then appends whatever it returned at the
/// version that was read - so a concurrent approval of the same order loses the race and retries
/// rather than overwriting.</para>
/// <para>Returning both an <see cref="Events"/> and an <see cref="OutgoingMessages"/> is what makes
/// the append and the publish atomic. Both go through the Marten/Wolverine outbox in one
/// transaction: if the commit fails nothing is sent, and if the process dies after the commit the
/// message is still in the outbox table and goes out on restart.</para>
/// <para>The method is static, takes everything it needs as parameters and touches no clock or
/// session directly, so the unit tests call it as a plain function with no host, no Postgres and no
/// Docker.</para>
/// </remarks>
public static class ApproveOrderHandler
{
    [AggregateHandler]
    public static (Events, OutgoingMessages) Handle(ApproveOrder command, Order order, TimeProvider clock)
    {
        order.EnsureCanBeApproved();

        var occurredAtUtc = clock.GetUtcNow();

        var approved = new OrderApproved(command.OrderId, occurredAtUtc);

        // Mapped explicitly rather than published as-is. The domain event stays inside the
        // aggregate boundary; what leaves the service is the versioned contract.
        var integrationEvent = new OrderApprovedV1(
            MessageId: Guid.NewGuid(),
            SchemaVersion: OrderApprovedV1.CurrentSchemaVersion,
            OccurredAtUtc: occurredAtUtc,
            OrderId: order.Id,
            Customer: order.Customer,
            Total: order.Total);

        return ([approved], [integrationEvent]);
    }
}
