using template.events.Projections;

namespace template.events.Abstractions;

/// <summary>
/// Reads the widget summary projection. One method, implemented once per event store.
/// </summary>
/// <remarks>
/// <para>The only thing <see cref="Controllers.WidgetsController"/> needs from a store is a
/// document load, and the three stores spell it with three different session types
/// (<c>Marten.IQuerySession</c>, <c>Polecat.IQuerySession</c>, <c>Fisher.IQuerySession</c>). This
/// interface is that one call, so the controller - the status codes, the route names, the OpenAPI
/// annotations and the eventual-consistency behaviour that goes with them - is written once instead
/// of three times.</para>
/// <para>It is not a repository and should not grow into one. If a host needs a query the
/// controller does not make, that query belongs in the host.</para>
/// </remarks>
public interface IWidgetSummaries
{
    Task<WidgetSummary?> LoadAsync(Guid id, CancellationToken cancellationToken);
}
