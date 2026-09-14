using Microsoft.Extensions.Logging;
using template.events.Contracts;

namespace template.events.Handlers;

/// <summary>
/// Consumes the integration event this service publishes.
/// </summary>
/// <remarks>
/// <para>A stand-in for the other component that would consume this contract in a real system.
/// With no broker configured, Wolverine routes <see cref="WidgetCompletedV1"/> to a durable local
/// queue, so this still exercises the whole outbox path - commit, store, dispatch, acknowledge -
/// and the hop shows up as its own span under the complete request in the dashboard's trace view.
/// Point the contract at a real transport later and nothing about this handler changes.</para>
/// <para>An instance handler rather than a static one, purely so the logger can arrive by
/// constructor injection; Wolverine resolves the class from the container either way.</para>
/// <para>Logging the fields rather than the whole message is deliberate: message bodies can carry
/// regulated data and do not belong in logs.</para>
/// </remarks>
public class WidgetCompletedV1Handler(ILogger<WidgetCompletedV1Handler> logger)
{
    public void Handle(WidgetCompletedV1 message) =>
        logger.LogInformation(
            "Integration event widget-completed-v1 received: widget {WidgetId}, schema {SchemaVersion}, completed at {OccurredAtUtc:O}.",
            message.WidgetId,
            message.SchemaVersion,
            message.OccurredAtUtc);
}
