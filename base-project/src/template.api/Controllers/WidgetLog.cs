namespace template.api.Controllers;

/// <summary>
/// Every log line <see cref="WidgetsController"/> writes, generated at compile time by
/// <c>[LoggerMessage]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Generated methods keep the message template constant and skip the work when the level is off,
/// which is what CA2254 and CA1848 (errors in Directory.Build.props) insist on. None of them takes the
/// id: the entity scope around the action already puts <c>entity.type</c> and <c>entity.id</c> on the
/// record.
/// </para>
/// <para>
/// Levels: writes and misses at Information - created, updated, deleted, not found, refused - and
/// reads at Debug. A resource that adds writes adds its Created, Updated and Deleted here; see
/// docs/ADDING-A-RESOURCE.md.
/// </para>
/// </remarks>
internal static partial class WidgetLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Widget found")]
    public static partial void WidgetFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Widget not found")]
    public static partial void WidgetNotFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listed {Count} widgets")]
    public static partial void WidgetsListed(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failing {Path} because a fault was injected.")]
    public static partial void FaultInjected(this ILogger logger, string path);
}
