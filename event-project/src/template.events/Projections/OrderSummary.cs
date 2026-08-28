using template.events.Domain;

namespace template.events.Projections;

/// <summary>
/// The read model. Shaped for the query that serves it, not for the write side's rules.
/// </summary>
/// <remarks>
/// Marten stores this as a document keyed by <see cref="Id"/>, which is the stream id, so a read is
/// a single-document load rather than a stream replay. That is the whole point of a projection: the
/// aggregate in <c>Domain/Order.cs</c> is rebuilt from events on every command, and this is not.
/// </remarks>
public sealed class OrderSummary
{
    public Guid Id { get; set; }

    public string Customer { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public OrderStatus Status { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }
}
