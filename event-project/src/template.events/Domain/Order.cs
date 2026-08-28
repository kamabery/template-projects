namespace template.events.Domain;

public enum OrderStatus
{
    Placed,
    Approved
}

/// <summary>
/// The write-side aggregate: current state, rebuilt from the stream on every command.
/// </summary>
/// <remarks>
/// <para>This type is never registered as a projection. Marten live-aggregates it on demand from
/// the <c>Apply</c> methods below, which is what <c>FetchForWriting&lt;Order&gt;</c> - the call
/// <c>[AggregateHandler]</c> generates - uses to load it. <see cref="OrderSummary"/> is the
/// registered projection, and it exists for reads only; keeping the two apart is what lets the read
/// model change shape without touching the rules here.</para>
/// <para><c>Apply</c> returns void and mutates in place. Marten also accepts <c>Apply</c> methods
/// that return a new instance; void is chosen here only because it is shorter to read.</para>
/// </remarks>
public sealed class Order
{
    public Guid Id { get; set; }

    public string Customer { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public OrderStatus Status { get; set; }

    public void Apply(OrderPlaced placed)
    {
        Id = placed.OrderId;
        Customer = placed.Customer;
        Total = placed.Total;
        Status = OrderStatus.Placed;
    }

    public void Apply(OrderApproved _) => Status = OrderStatus.Approved;

    /// <summary>
    /// The invariant. Throwing here is what makes approving twice a no-op rather than a second
    /// event on the stream, which matters because delivery is at-least-once and a redelivered
    /// ApproveOrder must not produce a second business effect.
    /// </summary>
    public void EnsureCanBeApproved()
    {
        if (Status == OrderStatus.Approved)
        {
            throw new InvalidOperationException($"Order {Id} has already been approved.");
        }
    }
}
