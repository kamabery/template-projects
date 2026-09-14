using template.events.Contracts;

namespace template.events.Domain;

/// <summary>
/// What each command decides, as plain functions over plain types.
/// </summary>
/// <remarks>
/// <para>This is the half of the handlers that has nothing to do with an event store. The handlers
/// themselves cannot live here - <c>[AggregateHandler]</c>, the <c>Events</c> return type and
/// <c>IDocumentSession</c> are all declared once per store, so each host has to bring its own - but
/// everything they decide can, and does. A host handler is then three lines: call one of these,
/// wrap the result in the store's types, return it.</para>
/// <para>The mapping in <see cref="Complete"/> is the reason this file exists rather than being
/// inlined three times. It is where a domain event becomes a versioned public contract, and three
/// copies of that rule drifting apart is exactly the failure this project is here to prevent.</para>
/// <para>Every method is static, takes its clock as a parameter and touches no session, so the unit
/// tests call them as plain functions with no host, no database and no Docker.</para>
/// </remarks>
public static class WidgetDecisions
{
    /// <summary>
    /// Decides a new widget's id and its first event. The caller starts the stream with them.
    /// </summary>
    public static (Guid WidgetId, WidgetCreated Created) Create(CreateWidget command, TimeProvider clock)
    {
        var widgetId = Guid.NewGuid();

        return (widgetId, new WidgetCreated(widgetId, command.Name, command.Quantity, clock.GetUtcNow()));
    }

    /// <summary>
    /// Checks the invariant and returns both the domain event to append and the integration event
    /// to publish, so the caller can hand them to the outbox in a single return.
    /// </summary>
    /// <exception cref="InvalidOperationException">The widget is already completed.</exception>
    public static (WidgetCompleted Completed, WidgetCompletedV1 Integration) Complete(
        Widget widget,
        CompleteWidget command,
        TimeProvider clock)
    {
        widget.EnsureCanBeCompleted();

        var occurredAtUtc = clock.GetUtcNow();

        var completed = new WidgetCompleted(command.WidgetId, occurredAtUtc);

        // Mapped explicitly rather than published as-is. The domain event stays inside the
        // aggregate boundary; what leaves the service is the versioned contract.
        var integrationEvent = new WidgetCompletedV1(
            MessageId: Guid.NewGuid(),
            SchemaVersion: WidgetCompletedV1.CurrentSchemaVersion,
            OccurredAtUtc: occurredAtUtc,
            WidgetId: widget.Id,
            Name: widget.Name,
            Quantity: widget.Quantity);

        return (completed, integrationEvent);
    }
}
