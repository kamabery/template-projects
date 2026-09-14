using Polecat;
using template.events.Abstractions;
using template.events.Projections;

namespace template.events.Storage;

/// <summary>
/// The Polecat half of <see cref="IWidgetSummaries"/>: a single-document load by stream id.
/// </summary>
/// <remarks>
/// Reads the projection document, not the stream. A miss right after a POST does not mean the widget
/// is absent - it means the async daemon has not caught up yet.
/// </remarks>
internal sealed class WidgetSummaries(IQuerySession query) : IWidgetSummaries
{
    public Task<WidgetSummary?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        query.LoadAsync<WidgetSummary>(id, cancellationToken);
}
