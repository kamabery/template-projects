namespace template.events.Domain;

/// <summary>
/// The domain events that make up a widget's stream.
/// </summary>
/// <remarks>
/// <para>Named in the past tense and immutable, per the spec's stream rules: an event is a fact
/// that already happened, so there is nothing to mutate. Records give that for free.</para>
/// <para>These are deliberately NOT public integration contracts. They live inside the aggregate
/// boundary and are free to change shape as the domain does; what other components consume is the
/// versioned contract in <c>Contracts/</c>, which a handler maps to explicitly.</para>
/// </remarks>
public sealed record WidgetCreated(Guid WidgetId, string Name, int Quantity, DateTimeOffset OccurredAtUtc);

public sealed record WidgetCompleted(Guid WidgetId, DateTimeOffset OccurredAtUtc);
