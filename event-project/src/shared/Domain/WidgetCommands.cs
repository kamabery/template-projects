namespace template.events.Domain;

/// <summary>
/// Requests that a handler do something. Unlike the events beside them these are not facts, and
/// they may be rejected - <see cref="Widget.EnsureCanBeCompleted"/> is where that happens.
/// </summary>
public sealed record CreateWidget(string Name, int Quantity);

public sealed record CompleteWidget(Guid WidgetId);
