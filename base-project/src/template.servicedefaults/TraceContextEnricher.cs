using System.Diagnostics;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds the current W3C trace context to every log event as named fields: <c>trace_id</c>,
/// <c>span_id</c>, <c>trace_flags</c>, and <c>parent_span_id</c> when the span has a parent.
/// </summary>
/// <remarks>
/// Application Insights already joins a log to its request through <c>operation_Id</c>, but the console
/// and the Aspire dashboard's structured view only show what is on the record itself. Enrichers run on
/// the logging thread, so <see cref="Activity.Current"/> is the caller's span. Outside any span the
/// event gets none of the four.
/// </remarks>
internal sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace_id", activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span_id", activity.SpanId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "trace_flags", ((int)activity.ActivityTraceFlags).ToString("x2", CultureInfo.InvariantCulture)));

        if (activity.ParentSpanId != default)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("parent_span_id", activity.ParentSpanId.ToHexString()));
        }
    }
}
