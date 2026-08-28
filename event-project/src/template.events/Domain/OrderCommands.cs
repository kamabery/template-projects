namespace template.events.Domain;

/// <summary>
/// Requests that a handler do something. Unlike the events beside them these are not facts, and
/// they may be rejected - <see cref="Order.EnsureCanBeApproved"/> is where that happens.
/// </summary>
public sealed record PlaceOrder(string Customer, decimal Total);

public sealed record ApproveOrder(Guid OrderId);
