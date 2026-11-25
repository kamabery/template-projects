using Microsoft.Extensions.Logging;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>A copy of one <c>LogRecord</c>'s level and scope values, taken before the SDK reuses it.</summary>
public sealed record CapturedScopeLog(LogLevel Level, IReadOnlyDictionary<string, object?> Scopes);
