using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Names the entity a block of work is about, on every log record inside it and on the current span,
/// as one fixed, generic pair: <c>entity.type</c> and <c>entity.id</c>, plus <c>entity.related</c> for
/// the other entities involved. One query then finds an entity's history across logs and spans,
/// whatever the resource is called.
/// </summary>
/// <remarks>
/// <para>
/// <c>entity.id</c> is the surrogate key - an int or a GUID - and always a string, so the query never
/// cares about the key's type. A composite key is joined with <c>|</c>. A natural key, such as an
/// email address, never goes in here: it would put personal data in every log store.
/// </para>
/// <para>
/// The scope is an ordinary <see cref="ILogger.BeginScope{TState}"/> with a key/value state, which
/// Serilog turns into <c>LogContext</c> properties; the OpenTelemetry forwarder hands them on as
/// attributes. The same values are set as tags on <see cref="Activity.Current"/>, so the request span
/// can be filtered by entity too. A child span started later, such as a database call, does not
/// inherit them; it joins through its operation id.
/// </para>
/// </remarks>
public static class EntityScope
{
    public const string TypeKey = "entity.type";
    public const string IdKey = "entity.id";
    public const string RelatedKey = "entity.related";

    /// <summary>Opens a scope for one entity, with any related ones as <c>(Type, Id)</c> pairs.</summary>
    public static IDisposable? ForEntity(this ILogger logger, string type, object id, params (string Type, object Id)[] related)
    {
        var state = new Dictionary<string, object> { [TypeKey] = type, [IdKey] = Format(id) };

        if (related.Length > 0)
        {
            state[RelatedKey] = related.Select(r => $"{r.Type}:{Format(r.Id)}").ToArray();
        }

        return Open(logger, state);
    }

    /// <summary>Opens a scope for a type with no single id, such as a list.</summary>
    public static IDisposable? ForEntity(this ILogger logger, string type) =>
        Open(logger, new Dictionary<string, object> { [TypeKey] = type });

    private static IDisposable? Open(ILogger logger, Dictionary<string, object> state)
    {
        var activity = Activity.Current;
        foreach (var (key, value) in state)
        {
            activity?.SetTag(key, value);
        }

        return logger.BeginScope(state);
    }

    private static string Format(object id) => id is ITuple tuple
        ? string.Join('|', Enumerable.Range(0, tuple.Length).Select(i => Format(tuple[i]!)))
        : Convert.ToString(id, CultureInfo.InvariantCulture)!;
}
