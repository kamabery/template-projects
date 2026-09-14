using template.events.Domain;
using Wolverine;
using Wolverine.Polecat;

namespace template.events.Handlers;

/// <summary>
/// The worked example: load an aggregate, check its invariants, decide what happened.
/// </summary>
/// <remarks>
/// <para><c>[AggregateHandler]</c> generates the surrounding workflow. It reads the stream id from
/// the command's <c>WidgetId</c> by convention, calls <c>FetchForWriting&lt;Widget&gt;</c> to
/// live-aggregate the current state, invokes this method, then appends whatever it returned at the
/// version that was read - so a concurrent completion of the same widget loses the race and retries
/// rather than overwriting.</para>
/// <para>Returning both an <see cref="Events"/> and an <see cref="OutgoingMessages"/> is what makes
/// the append and the publish atomic. Both go through the Polecat/Wolverine outbox in one
/// transaction: if the commit fails nothing is sent, and if the process dies after the commit the
/// message is still in the outbox table and goes out on restart.</para>
/// <para>The attribute and both return types are declared by <c>Wolverine.Polecat</c>, which is why
/// this file cannot be shared with the other event-store hosts even though the rule it applies can:
/// everything it decides is in <see cref="WidgetDecisions.Complete"/>.</para>
/// </remarks>
public static class CompleteWidgetHandler
{
    [AggregateHandler]
    public static (Events, OutgoingMessages) Handle(CompleteWidget command, Widget widget, TimeProvider clock)
    {
        var (completed, integrationEvent) = WidgetDecisions.Complete(widget, command, clock);

        return ([completed], [integrationEvent]);
    }
}
