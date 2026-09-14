using Fisher;
using template.events.Domain;

namespace template.events.Handlers;

/// <summary>
/// Starts a new widget stream.
/// </summary>
/// <remarks>
/// <para>No <c>[AggregateHandler]</c> here, unlike <see cref="CompleteWidgetHandler"/>: there is no
/// existing aggregate to load and no expected version to enforce, because this call is what brings
/// the stream into being. <c>StartStream</c> fails if the id is already taken, which is the
/// concurrency protection appropriate to a creation.</para>
/// <para>The decision itself lives in <see cref="WidgetDecisions.Create"/>, shared with the other
/// event-store hosts. All that is left here is the one line only Fisher can spell.</para>
/// </remarks>
public static class CreateWidgetHandler
{
    public static Guid Handle(CreateWidget command, IDocumentSession session, TimeProvider clock)
    {
        var (widgetId, created) = WidgetDecisions.Create(command, clock);

        session.Events.StartStream<Widget>(widgetId, created);

        // No SaveChangesAsync. Policies.AutoApplyTransactions() in Program.cs owns the transaction
        // and commits it around this handler; calling it here would commit early and take the
        // outgoing messages out of the same unit of work.
        return widgetId;
    }
}
