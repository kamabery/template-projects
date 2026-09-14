using template.events.Domain;

namespace template.events.Projections;

/// <summary>
/// The read model. Shaped for the query that serves it, not for the write side's rules.
/// </summary>
/// <remarks>
/// <para>The event store keeps this as a document keyed by <see cref="Id"/>, which is the stream
/// id, so a read is a single-document load rather than a stream replay. That is the whole point of
/// a projection: the aggregate in <c>Domain/Widget.cs</c> is rebuilt from events on every command,
/// and this is not.</para>
/// <para>The two methods below are the projection's actual behaviour. They live on the read model
/// rather than in the projection class because <c>SingleStreamProjection&lt;,&gt;</c> is declared
/// once per event store, so each host has to bring its own projection type - and those types are
/// then a two-line adapter over these, instead of three copies of the same rules.</para>
/// <para>Both are deterministic and depend only on their event, which is what makes a rebuild from
/// the stream produce the same document as the original run.</para>
/// </remarks>
public sealed class WidgetSummary
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public WidgetStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Creates the summary from the event that started the stream.</summary>
    public static WidgetSummary From(WidgetCreated created) => new()
    {
        Id = created.WidgetId,
        Name = created.Name,
        Quantity = created.Quantity,
        Status = WidgetStatus.Created,
        CreatedAtUtc = created.OccurredAtUtc
    };

    /// <summary>Applies the completion to an existing summary.</summary>
    public void ApplyCompleted(WidgetCompleted completed)
    {
        Status = WidgetStatus.Completed;
        CompletedAtUtc = completed.OccurredAtUtc;
    }
}
