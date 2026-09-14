using Wolverine.Attributes;

namespace template.events.Contracts;

/// <summary>
/// The public, versioned fact that a widget was completed.
/// </summary>
/// <remarks>
/// <para>Separate from the <c>WidgetCompleted</c> domain event on purpose. The domain event is
/// internal to the aggregate and free to change; this is a cross-component contract, so a breaking
/// change means a new type (<c>WidgetCompletedV2</c>) rather than an edited one.</para>
/// <para><see cref="MessageIdentityAttribute"/> is what makes the wire name stable and independent
/// of the CLR assembly-qualified name - rename the class or move it to another assembly and
/// consumers still resolve it. Without it, Wolverine falls back to the type name and a refactor
/// silently becomes a breaking change.</para>
/// <para>Only the fields Wolverine's envelope does NOT already carry are declared here.
/// CorrelationId, CausationId and traceparent ride on the envelope and are visible in the
/// dashboard's trace view, so duplicating them in the body would just be two places to disagree.
/// MessageId and OccurredAtUtc are carried explicitly because a consumer that persists this
/// contract needs them after the envelope is gone.</para>
/// </remarks>
[MessageIdentity("widget-completed-v1")]
public sealed record WidgetCompletedV1(
    Guid MessageId,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    Guid WidgetId,
    string Name,
    int Quantity)
{
    /// <summary>The version this type represents. Bumped only by introducing a new type.</summary>
    public const int CurrentSchemaVersion = 1;
}
