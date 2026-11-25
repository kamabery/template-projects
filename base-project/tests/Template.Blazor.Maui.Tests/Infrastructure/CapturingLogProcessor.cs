using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// Records what reaches the OpenTelemetry logging pipeline, with the values of every scope the record
/// was written in - what the OTLP exporters read from.
/// </summary>
public sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
{
    private readonly ConcurrentQueue<CapturedScopeLog> _records = new();

    public IReadOnlyCollection<CapturedScopeLog> Records => _records;

    public override void OnEnd(LogRecord data)
    {
        var scopes = new Dictionary<string, object?>();
        data.ForEachScope(static (scope, values) =>
        {
            foreach (var (key, value) in scope)
            {
                values[key] = value;
            }
        }, scopes);

        _records.Enqueue(new CapturedScopeLog(data.LogLevel, scopes));
    }
}
