using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// A Serilog sink that hands every event to the OpenTelemetry logging provider, so Serilog stays
/// the front door for structured logging and OpenTelemetry is the one way out - to the dashboard
/// over OTLP, and to Application Insights through the Azure Monitor distro.
/// </summary>
/// <remarks>
/// <para>
/// Not <c>AddSerilog(writeToProviders: true)</c>: that bridge passes the Serilog <c>LogEvent</c>
/// itself as the log state, which the OpenTelemetry provider cannot read, so every property would
/// be lost. Here the state is the key/value list the provider expects, so named properties and
/// <c>LogContext</c> enrichments become attributes - <c>customDimensions</c> in Application Insights.
/// </para>
/// <para>
/// Scalars pass through as they are. Anything nested - a destructured <c>{@Order}</c>, a sequence, a
/// dictionary - becomes one JSON string, because log attributes are flat; KQL reads it back with
/// <c>parse_json</c>. The event is forwarded on the logging thread, so the provider stamps the
/// current Activity's trace id on it and the log joins its request's transaction.
/// </para>
/// </remarks>
internal sealed class OpenTelemetryLogForwarder(IEnumerable<ILoggerProvider> providers) : ILogEventSink
{
    private const string SourceContext = "SourceContext";
    private const string OriginalFormat = "{OriginalFormat}";

    // No type tags: the JSON should read as the object, not as Serilog's description of it.
    private static readonly JsonValueFormatter Json = new(typeTagName: null);

    private readonly ILoggerProvider[] _providers = providers.ToArray();
    private readonly ConcurrentDictionary<string, ILogger[]> _loggers = new();

    public void Emit(LogEvent logEvent)
    {
        if (_providers.Length == 0)
        {
            return;
        }

        var category = logEvent.Properties.TryGetValue(SourceContext, out var source)
            && source is ScalarValue { Value: string name }
                ? name
                : "None";

        var state = ToState(logEvent);
        var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        var level = ToLevel(logEvent.Level);

        foreach (var logger in _loggers.GetOrAdd(category, c => _providers.Select(p => p.CreateLogger(c)).ToArray()))
        {
            logger.Log(level, default, state, logEvent.Exception, (_, _) => message);
        }
    }

    private static List<KeyValuePair<string, object?>> ToState(LogEvent logEvent)
    {
        var state = new List<KeyValuePair<string, object?>>(logEvent.Properties.Count + 1);

        foreach (var (key, value) in logEvent.Properties)
        {
            // The category already carries it.
            if (key == SourceContext)
            {
                continue;
            }

            state.Add(new KeyValuePair<string, object?>(key, ToValue(value)));
        }

        // Last, by the convention ILogger's own state follows; the provider reads it as the body.
        state.Add(new KeyValuePair<string, object?>(OriginalFormat, logEvent.MessageTemplate.Text));
        return state;
    }

    private static object? ToValue(LogEventPropertyValue value)
    {
        if (value is ScalarValue scalar)
        {
            return scalar.Value;
        }

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Json.Format(value, writer);
        return writer.ToString();
    }

    private static LogLevel ToLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        _ => LogLevel.Critical,
    };
}
