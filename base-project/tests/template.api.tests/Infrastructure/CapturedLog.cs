using Microsoft.Extensions.Logging;

namespace template.api.tests.Infrastructure;

/// <summary>A copy of one <c>LogRecord</c>, taken before the SDK returns it to its pool.</summary>
public sealed record CapturedLog(
    string? Category,
    LogLevel Level,
    string TraceId,
    IReadOnlyDictionary<string, object?> Attributes);
