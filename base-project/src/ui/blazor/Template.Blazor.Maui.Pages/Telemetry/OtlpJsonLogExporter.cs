using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// Sends log records to the API's relay as OTLP/HTTP JSON - the log twin of
/// <see cref="OtlpJsonTraceExporter"/>. The relay writes them through its own ILogger under the
/// Browser category, with the trace id each was written under.
/// </summary>
public sealed class OtlpJsonLogExporter(Func<HttpClient> createClient, Uri relayEndpoint) : BaseExporter<LogRecord>
{
    private const string OriginalFormat = "{OriginalFormat}";

    private readonly Uri _url = new($"{relayEndpoint.AbsoluteUri.TrimEnd('/')}/v1/logs");

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("resourceLogs");
            writer.WriteStartObject();
            writer.WriteStartArray("scopeLogs");
            writer.WriteStartObject();
            writer.WriteStartArray("logRecords");

            foreach (var record in batch)
            {
                Write(writer, record);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        OtlpJson.PostInBackground(createClient, _url, buffer.ToArray());
        return ExportResult.Success;
    }

    private static void Write(Utf8JsonWriter writer, LogRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("timeUnixNano", OtlpJson.UnixNanos(record.Timestamp));
        writer.WriteNumber("severityNumber", ToSeverityNumber(record.LogLevel));
        writer.WriteString("severityText", record.LogLevel.ToString());

        writer.WritePropertyName("body");
        OtlpJson.WriteAnyValue(writer, record.FormattedMessage ?? record.Body ?? string.Empty);

        var attributes = (record.Attributes ?? [])
            .Where(attribute => attribute.Key != OriginalFormat)
            .ToList();
        if (record.Exception is { } exception)
        {
            attributes.Add(new("exception.type", exception.GetType().FullName));
            attributes.Add(new("exception.message", exception.Message));
            attributes.Add(new("exception.stacktrace", exception.ToString()));
        }

        OtlpJson.WriteAttributes(writer, attributes);

        if (record.TraceId != default)
        {
            writer.WriteString("traceId", record.TraceId.ToHexString());
            writer.WriteString("spanId", record.SpanId.ToHexString());
        }

        writer.WriteEndObject();
    }

    // OTLP severity numbers come in fours: TRACE 1, DEBUG 5, INFO 9, WARN 13, ERROR 17, FATAL 21.
    private static int ToSeverityNumber(LogLevel level) => level switch
    {
        LogLevel.Trace => 1,
        LogLevel.Debug => 5,
        LogLevel.Information => 9,
        LogLevel.Warning => 13,
        LogLevel.Error => 17,
        LogLevel.Critical => 21,
        _ => 0,
    };
}
