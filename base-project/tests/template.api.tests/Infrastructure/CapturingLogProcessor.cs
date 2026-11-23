using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace template.api.tests.Infrastructure;

/// <summary>
/// Records what reaches the OpenTelemetry logging pipeline - the point every exporter, the
/// dashboard's and Azure Monitor's alike, reads from.
/// </summary>
public sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
{
    private readonly ConcurrentQueue<CapturedLog> _records = new();

    public IReadOnlyCollection<CapturedLog> Records => _records;

    public override void OnEnd(LogRecord data) =>
        // LogRecords are pooled and reused once this returns, so copy what the tests read.
        _records.Enqueue(new CapturedLog(
            data.CategoryName,
            data.LogLevel,
            data.TraceId.ToHexString(),
            data.Attributes?.ToDictionary(a => a.Key, a => a.Value) ?? []));
}
