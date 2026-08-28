using Wolverine.Attributes;

namespace template.events.Contracts;

/// <summary>
/// The public, versioned fact that an order was approved.
/// </summary>
/// <remarks>
/// <para>Separate from the <c>OrderApproved</c> domain event on purpose. The domain event is
/// internal to the aggregate and free to change; this is a cross-component contract, so a breaking
/// change means a new type (<c>OrderApprovedV2</c>) rather than an edited one.</para>
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
[MessageIdentity("order-approved-v1")]
public sealed record OrderApprovedV1(
    Guid MessageId,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    Guid OrderId,
    string Customer,
    decimal Total)
{
    /// <summary>The version this type represents. Bumped only by introducing a new type.</summary>
    public const int CurrentSchemaVersion = 1;
}
