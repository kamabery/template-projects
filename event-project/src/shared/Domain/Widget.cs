namespace template.events.Domain;

public enum WidgetStatus
{
    Created,
    Completed
}

/// <summary>
/// The write-side aggregate: current state, rebuilt from the stream on every command.
/// </summary>
/// <remarks>
/// <para>This type is never registered as a projection. The event store live-aggregates it on
/// demand from the <c>Apply</c> methods below, which is what <c>FetchForWriting&lt;Widget&gt;</c> -
/// the call <c>[AggregateHandler]</c> generates - uses to load it. <see cref="WidgetSummary"/> is
/// the registered projection, and it exists for reads only; keeping the two apart is what lets the
/// read model change shape without touching the rules here.</para>
/// <para><c>Apply</c> returns void and mutates in place. The store also accepts <c>Apply</c>
/// methods that return a new instance; void is chosen here only because it is shorter to read.</para>
/// <para>A deliberately generic two-state lifecycle - created, then completed. It is the shape of
/// an event-sourced aggregate rather than a domain worth keeping: rename it, give it the states
/// your own process actually has, and the handlers and projection follow.</para>
/// </remarks>
public sealed class Widget
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public WidgetStatus Status { get; set; }

    public void Apply(WidgetCreated created)
    {
        Id = created.WidgetId;
        Name = created.Name;
        Quantity = created.Quantity;
        Status = WidgetStatus.Created;
    }

    public void Apply(WidgetCompleted _) => Status = WidgetStatus.Completed;

    /// <summary>
    /// The invariant. Throwing here is what makes completing twice a no-op rather than a second
    /// event on the stream, which matters because delivery is at-least-once and a redelivered
    /// CompleteWidget must not produce a second business effect.
    /// </summary>
    public void EnsureCanBeCompleted()
    {
        if (Status == WidgetStatus.Completed)
        {
            throw new InvalidOperationException($"Widget {Id} has already been completed.");
        }
    }
}
