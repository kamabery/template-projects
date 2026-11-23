using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Telemetry;

/// <summary>
/// The WebAssembly host's Azure path: the .NET OTLP exporter has no JSON mode, and the API's relay
/// takes OTLP/HTTP JSON only, so these two write it by hand.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class OtlpJsonExporterTests
{
    private static readonly Uri Relay = new("https://api.example.test/otel");

    [Fact]
    public async Task TraceExporter_PostsTheSpanAsOtlpJson_ToTheRelaysTracesPath()
    {
        var handler = new CapturingHttpHandler();
        using var exporter = new OtlpJsonTraceExporter(() => new HttpClient(handler), Relay);

        using var source = new ActivitySource($"test-{Guid.NewGuid():N}");
        using var listener = ListenTo(source);
        var start = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        var activity = source.StartActivity("GET widgets", ActivityKind.Client, default(ActivityContext), startTime: start)!;
        activity.SetTag("http.status_code", 500);
        activity.SetTag("retry", true);
        activity.SetTag("url.path", "/widgets");
        activity.SetStatus(ActivityStatusCode.Error, "server error");
        activity.SetEndTime(start.AddMilliseconds(250));
        activity.Stop();

        exporter.Export(new Batch<Activity>([activity], 1)).ShouldBe(ExportResult.Success);

        var (url, body) = await handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        url.ShouldBe(new Uri("https://api.example.test/otel/v1/traces"));

        var span = JsonDocument.Parse(body).RootElement
            .GetProperty("resourceSpans")[0].GetProperty("scopeSpans")[0].GetProperty("spans")[0];

        span.ShouldSatisfyAllConditions(
            () => span.GetProperty("traceId").GetString().ShouldBe(activity.TraceId.ToHexString()),
            () => span.GetProperty("spanId").GetString().ShouldBe(activity.SpanId.ToHexString()),
            () => span.GetProperty("name").GetString().ShouldBe("GET widgets"),
            // OTLP numbers its kinds from 1 = Internal; Client is 3.
            () => span.GetProperty("kind").GetInt32().ShouldBe(3),
            () => span.GetProperty("startTimeUnixNano").GetString().ShouldBe("1795428000000000000"),
            () => span.GetProperty("endTimeUnixNano").GetString().ShouldBe("1795428000250000000"),
            () => span.GetProperty("status").GetProperty("code").GetInt32().ShouldBe(2),
            () => span.GetProperty("status").GetProperty("message").GetString().ShouldBe("server error"),
            () => Attribute(span, "http.status_code").GetProperty("intValue").GetString().ShouldBe("500"),
            () => Attribute(span, "retry").GetProperty("boolValue").GetBoolean().ShouldBeTrue(),
            () => Attribute(span, "url.path").GetProperty("stringValue").GetString().ShouldBe("/widgets"));
    }

    [Fact]
    public async Task LogExporter_PostsTheRecordAsOtlpJson_ToTheRelaysLogsPath()
    {
        var handler = new CapturingHttpHandler();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(otel =>
        {
            otel.IncludeFormattedMessage = true;
            otel.AddProcessor(new SimpleLogRecordExportProcessor(
                new OtlpJsonLogExporter(() => new HttpClient(handler), Relay)));
        }));

        loggerFactory.CreateLogger("Widgets").LogWarning("Widget {WidgetId} is out of stock", 42);

        var (url, body) = await handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        url.ShouldBe(new Uri("https://api.example.test/otel/v1/logs"));

        var record = JsonDocument.Parse(body).RootElement
            .GetProperty("resourceLogs")[0].GetProperty("scopeLogs")[0].GetProperty("logRecords")[0];

        record.ShouldSatisfyAllConditions(
            // OTLP severity numbers come in fours; WARN starts at 13.
            () => record.GetProperty("severityNumber").GetInt32().ShouldBe(13),
            () => record.GetProperty("body").GetProperty("stringValue").GetString().ShouldBe("Widget 42 is out of stock"),
            () => Attribute(record, "WidgetId").GetProperty("intValue").GetString().ShouldBe("42"));
    }

    private static JsonElement Attribute(JsonElement item, string key) =>
        item.GetProperty("attributes").EnumerateArray()
            .Single(attribute => attribute.GetProperty("key").GetString() == key)
            .GetProperty("value");

    private static ActivityListener ListenTo(ActivitySource source)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
