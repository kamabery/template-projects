using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// Sends spans to the API's relay as OTLP/HTTP JSON: the WebAssembly host's way to Application
/// Insights in Azure, where there is no dashboard.
/// </summary>
/// <remarks>
/// Hand-written because the .NET OTLP exporter has no JSON mode and the relay takes JSON only. It
/// writes what the relay reads - ids, name, kind, times, status, attributes - and nothing else.
/// </remarks>
public sealed class OtlpJsonTraceExporter(Func<HttpClient> createClient, Uri relayEndpoint) : BaseExporter<Activity>
{
    private readonly Uri _url = new($"{relayEndpoint.AbsoluteUri.TrimEnd('/')}/v1/traces");

    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("resourceSpans");
            writer.WriteStartObject();
            writer.WriteStartArray("scopeSpans");
            writer.WriteStartObject();
            writer.WriteStartArray("spans");

            foreach (var activity in batch)
            {
                Write(writer, activity);
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

    private static void Write(Utf8JsonWriter writer, Activity activity)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", activity.TraceId.ToHexString());
        writer.WriteString("spanId", activity.SpanId.ToHexString());
        if (activity.ParentSpanId != default)
        {
            writer.WriteString("parentSpanId", activity.ParentSpanId.ToHexString());
        }

        writer.WriteString("name", activity.DisplayName);
        // OTLP numbers its kinds from 1 = Internal; ActivityKind starts at Internal = 0.
        writer.WriteNumber("kind", (int)activity.Kind + 1);
        writer.WriteString("startTimeUnixNano", OtlpJson.UnixNanos(activity.StartTimeUtc));
        writer.WriteString("endTimeUnixNano", OtlpJson.UnixNanos(activity.StartTimeUtc + activity.Duration));
        OtlpJson.WriteAttributes(writer, activity.TagObjects);

        writer.WriteStartObject("status");
        writer.WriteNumber("code", activity.Status switch
        {
            ActivityStatusCode.Ok => 1,
            ActivityStatusCode.Error => 2,
            _ => 0,
        });
        if (activity.StatusDescription is { } message)
        {
            writer.WriteString("message", message);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
