using System.Diagnostics;
using System.Text.Json;

namespace template.api.Telemetry;

/// <summary>
/// Turns a browser's OTLP/HTTP JSON into this API's own telemetry, so whatever exports the API's -
/// the Azure Monitor distro in Azure - exports the browser's with it. The connection string never
/// reaches the bundle.
/// </summary>
/// <remarks>
/// <para>
/// Spans are re-created on this API's <see cref="ActivitySource"/> (the one servicedefaults already
/// traces), keeping the browser's trace id, times, kind, status and attributes. The trace id is what
/// joins the page's span to the API request it caused and that request's database call: one
/// <c>operation_Id</c> in Application Insights. An Activity cannot be given a span id, so each gets
/// a new one; its parent is the browser span's own parent (or, for a root, the browser span itself),
/// so links inside the page are approximate and the trace is exact.
/// </para>
/// <para>
/// Log records go through <see cref="ILogger"/> under <see cref="LogCategory"/>, so they pass
/// Serilog's levels, sinks and enrichers like any other log. The browser's trace and span ids are
/// made current while each is written, which is where the log pipeline reads them from.
/// </para>
/// <para>Everything relayed carries <see cref="SourceAttribute"/> = <see cref="SourceValue"/>.</para>
/// </remarks>
public sealed class BrowserTelemetryRelay(string activitySourceName, ILoggerFactory loggerFactory) : IDisposable
{
    public const string LogCategory = "Browser";
    public const string SourceAttribute = "relay.source";
    public const string SourceValue = "browser";

    /// <summary>The property a relayed log's text is kept in. The text is a value, never a message
    /// template, so braces a browser logged stay as they were.</summary>
    public const string MessageAttribute = "BrowserMessage";

    private const string OriginalFormat = "{OriginalFormat}";

    private readonly ActivitySource _source = new(activitySourceName);
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory);

    public void RelaySpans(JsonElement exportRequest)
    {
        var current = Activity.Current;
        try
        {
            foreach (var span in OtlpJson.Nested(exportRequest, "resourceSpans", "scopeSpans", "spans"))
            {
                RelaySpan(span);
            }
        }
        finally
        {
            // A relayed span has a remote parent, so stopping it does not hand Current back to the
            // relay's own request.
            Activity.Current = current;
        }
    }

    public void RelayLogs(JsonElement exportRequest)
    {
        var current = Activity.Current;
        try
        {
            foreach (var record in OtlpJson.Nested(exportRequest, "resourceLogs", "scopeLogs", "logRecords"))
            {
                RelayLog(record);
            }
        }
        finally
        {
            Activity.Current = current;
        }
    }

    public void Dispose() => _source.Dispose();

    private void RelaySpan(JsonElement span)
    {
        if (OtlpJson.TraceId(span) is not { } traceId
            || OtlpJson.SpanId(span, "spanId") is not { } spanId
            || OtlpJson.Time(span, "startTimeUnixNano") is not { } start)
        {
            return;
        }

        var parent = new ActivityContext(
            traceId,
            OtlpJson.SpanId(span, "parentSpanId") ?? spanId,
            ActivityTraceFlags.Recorded,
            isRemote: true);

        using var activity = _source.StartActivity(
            OtlpJson.String(span, "name") ?? "browser span",
            ToKind(OtlpJson.Int64(span, "kind")),
            parent,
            OtlpJson.Attributes(span),
            startTime: start);

        if (activity is null)
        {
            return;
        }

        activity.SetTag(SourceAttribute, SourceValue);

        if (span.TryGetProperty("status", out var status))
        {
            activity.SetStatus(ToStatus(OtlpJson.Int64(status, "code")), OtlpJson.String(status, "message"));
        }

        activity.SetEndTime((OtlpJson.Time(span, "endTimeUnixNano") ?? start).UtcDateTime);
    }

    private void RelayLog(JsonElement record)
    {
        var level = ToLevel(OtlpJson.Int64(record, "severityNumber"));
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        var body = record.TryGetProperty("body", out var value) ? OtlpJson.Value(value) : null;

        var state = OtlpJson.Attributes(record).ToList();
        state.Add(new(SourceAttribute, SourceValue));
        state.Add(new(MessageAttribute, body?.ToString() ?? string.Empty));
        state.Add(new(OriginalFormat, $"{{{MessageAttribute}}}"));

        // A bare Activity, not one from a source: it is never exported, it only carries the
        // browser's ids to the log pipeline while this record is written.
        using var carrier = OtlpJson.TraceId(record) is { } traceId
            ? new Activity(LogCategory)
                .SetParentId(traceId, OtlpJson.SpanId(record, "spanId") ?? default, ActivityTraceFlags.Recorded)
                .Start()
            : null;

        _logger.Log(level, default, state, null, (_, _) => body?.ToString() ?? string.Empty);
    }

    // OTLP numbers its kinds from 1 (Internal) with 0 unspecified; ActivityKind starts at Internal.
    private static ActivityKind ToKind(long kind) => kind is >= 1 and <= 5 ? (ActivityKind)(kind - 1) : ActivityKind.Internal;

    private static ActivityStatusCode ToStatus(long code) => code switch
    {
        1 => ActivityStatusCode.Ok,
        2 => ActivityStatusCode.Error,
        _ => ActivityStatusCode.Unset,
    };

    // OTLP severity numbers come in fours: 1-4 TRACE, 5-8 DEBUG, 9-12 INFO, 13-16 WARN, 17-20 ERROR,
    // 21-24 FATAL. Unspecified (0) is taken as Information.
    private static LogLevel ToLevel(long severityNumber) => severityNumber switch
    {
        >= 21 => LogLevel.Critical,
        >= 17 => LogLevel.Error,
        >= 13 => LogLevel.Warning,
        >= 9 => LogLevel.Information,
        >= 5 => LogLevel.Debug,
        >= 1 => LogLevel.Trace,
        _ => LogLevel.Information,
    };
}
