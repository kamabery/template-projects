using Fisher.Projections;
using template.events.Domain;

namespace template.events.Projections;

/// <summary>
/// Builds <see cref="WidgetSummary"/> from one widget's stream.
/// </summary>
/// <remarks>
/// <para>Registered as <c>ProjectionLifecycle.Async</c> in Program.cs, so the async daemon applies
/// these events on a background thread with its own checkpoint. That is what makes the read model
/// independently rebuildable and gives it a lag metric.</para>
/// <para>The consequence is eventual consistency: a GET issued immediately after the POST that
/// completed a widget can still see the old status, or nothing at all for a just-created widget. That
/// is correct behaviour, not a race to fix. If a scaffolded project needs read-your-writes here
/// instead, change the lifecycle to <c>ProjectionLifecycle.Inline</c> in Program.cs and drop the
/// AddAsyncDaemon call - at the cost of doing the projection work inside the write transaction.
/// </para>
/// <para>The bodies delegate to <see cref="WidgetSummary"/>. <c>SingleStreamProjection&lt;,&gt;</c>
/// is declared once per event store, so this adapter has to be per-host; the rules it applies do
/// not.</para>
/// </remarks>
public sealed class WidgetSummaryProjection : SingleStreamProjection<WidgetSummary, Guid>
{
    public static WidgetSummary Create(WidgetCreated created) => WidgetSummary.From(created);

    public static void Apply(WidgetCompleted completed, WidgetSummary summary) => summary.ApplyCompleted(completed);
}
