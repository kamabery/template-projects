using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// The client half of the API's entity scope: names the entity a block of work is about, with the
/// same keys the API logs, <c>entity.type</c> and <c>entity.id</c> (always a string). Every log
/// inside the scope carries them, so one query finds an entity's history from the page to the
/// database.
/// </summary>
/// <remarks>
/// A plain <see cref="ILogger.BeginScope{TState}"/>: the logging pipeline here has
/// <c>IncludeScopes</c> on, and both exporters - OTLP to the dashboard and
/// <see cref="OtlpJsonLogExporter"/> to the relay - write scope values as attributes. The id is the
/// surrogate key, never a natural one such as an email.
/// </remarks>
public static class EntityScope
{
    public const string TypeKey = "entity.type";
    public const string IdKey = "entity.id";

    /// <summary>Opens a scope for one entity.</summary>
    public static IDisposable? ForEntity(this ILogger logger, string type, object id) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            [TypeKey] = type,
            [IdKey] = Convert.ToString(id, CultureInfo.InvariantCulture)!,
        });

    /// <summary>Opens a scope for a type with no single id, such as a list.</summary>
    public static IDisposable? ForEntity(this ILogger logger, string type) =>
        logger.BeginScope(new Dictionary<string, object> { [TypeKey] = type });
}
