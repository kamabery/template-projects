using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using template.api.Telemetry;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// The /api/otel relay re-creates a browser's OTLP/HTTP JSON spans as the API's own Activities, so
/// the Azure Monitor distro exports them beside the request they led to, and writes its log records
/// through ILogger under the Browser category.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class BrowserTelemetryRelayTests : IDisposable
{
    private const string TraceId = "5b8efff798038103d269b633813fc60c";
    private const string ParentSpanId = "eee19b7ec3c1b173";
    private const string SpanId = "eee19b7ec3c1b174";

    // 2026-11-23T10:00:00Z and 250 ms later, in Unix nanoseconds - the form OTLP JSON carries them in.
    private const long StartNanos = 1_795_428_000_000_000_000;
    private const long EndNanos = StartNanos + 250_000_000;

    private readonly string _sourceName = $"relay-test-{Guid.NewGuid():N}";
    private readonly ConcurrentQueue<Activity> _spans = new();
    private readonly ActivityListener _listener;
    private readonly CapturingLogProcessor _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly BrowserTelemetryRelay _relay;

    public BrowserTelemetryRelayTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == _sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _spans.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);

        _loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(otel => otel.AddProcessor(_logs)));

        _relay = new BrowserTelemetryRelay(_sourceName, _loggerFactory);
    }

    public void Dispose()
    {
        _relay.Dispose();
        _loggerFactory.Dispose();
        _listener.Dispose();
    }

    [Fact]
    public void Span_KeepsItsTraceIdTimesStatusAndAttributes()
    {
        _relay.RelaySpans(Json($$$"""
            {"resourceSpans":[{"resource":{"attributes":[]},"scopeSpans":[{"scope":{"name":"fetch"},"spans":[{
              "traceId":"{{{TraceId}}}","spanId":"{{{SpanId}}}","parentSpanId":"{{{ParentSpanId}}}",
              "name":"HTTP POST","kind":3,
              "startTimeUnixNano":"{{{StartNanos}}}","endTimeUnixNano":"{{{EndNanos}}}",
              "attributes":[
                {"key":"http.url","value":{"stringValue":"/api/widgets"}},
                {"key":"http.status_code","value":{"intValue":"500"}},
                {"key":"retry","value":{"boolValue":true}}],
              "status":{"code":2,"message":"server error"}}]}]}]}
            """));

        var span = _spans.ShouldHaveSingleItem();
        span.ShouldSatisfyAllConditions(
            () => span.TraceId.ToHexString().ShouldBe(TraceId),
            () => span.DisplayName.ShouldBe("HTTP POST"),
            () => span.Kind.ShouldBe(ActivityKind.Client),
            () => span.StartTimeUtc.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(StartNanos / 1_000_000).UtcDateTime),
            () => span.Duration.ShouldBe(TimeSpan.FromMilliseconds(250)),
            () => span.Status.ShouldBe(ActivityStatusCode.Error),
            () => span.StatusDescription.ShouldBe("server error"),
            () => span.GetTagItem("http.url").ShouldBe("/api/widgets"),
            () => span.GetTagItem("http.status_code").ShouldBe(500L),
            () => span.GetTagItem("retry").ShouldBe(true),
            () => span.GetTagItem(BrowserTelemetryRelay.SourceAttribute).ShouldBe(BrowserTelemetryRelay.SourceValue));
    }

    [Fact]
    public void Span_GetsANewSpanId_UnderItsBrowserParent()
    {
        _relay.RelaySpans(Json(Spans(("click", TraceId, SpanId, ParentSpanId))));

        var span = _spans.ShouldHaveSingleItem();
        span.SpanId.ToHexString().ShouldNotBe(SpanId);
        span.ParentSpanId.ToHexString().ShouldBe(ParentSpanId);
    }

    [Fact]
    public void RootSpan_StaysInItsTrace()
    {
        _relay.RelaySpans(Json(Spans(("documentLoad", TraceId, SpanId, ""))));

        _spans.ShouldHaveSingleItem().TraceId.ToHexString().ShouldBe(TraceId);
    }

    [Fact]
    public void Span_WithAnInvalidTraceId_IsSkipped_AndTheRestAreKept()
    {
        _relay.RelaySpans(Json(Spans(("bad", "not-hex", SpanId, ""), ("good", TraceId, SpanId, ""))));

        _spans.ShouldHaveSingleItem().DisplayName.ShouldBe("good");
    }

    [Fact]
    public void Relaying_LeavesTheCurrentActivityAlone()
    {
        using var request = new Activity("POST /otel/v1/traces").Start();

        _relay.RelaySpans(Json(Spans(("click", TraceId, SpanId, ParentSpanId))));

        Activity.Current.ShouldBeSameAs(request);
    }

    [Fact]
    public void LogRecord_IsWrittenUnderBrowser_WithItsLevelAttributesAndTraceId()
    {
        _relay.RelayLogs(Json($$$"""
            {"resourceLogs":[{"resource":{"attributes":[]},"scopeLogs":[{"scope":{"name":"browser"},"logRecords":[{
              "timeUnixNano":"{{{StartNanos}}}","severityNumber":17,"severityText":"Error",
              "body":{"stringValue":"Widget {id} failed"},
              "attributes":[{"key":"exception.type","value":{"stringValue":"TypeError"}}],
              "traceId":"{{{TraceId}}}","spanId":"{{{SpanId}}}"}]}]}]}
            """));

        var log = _logs.Records.ShouldHaveSingleItem();
        log.ShouldSatisfyAllConditions(
            () => log.Category.ShouldBe(BrowserTelemetryRelay.LogCategory),
            () => log.Level.ShouldBe(LogLevel.Error),
            () => log.TraceId.ShouldBe(TraceId),
            // Braces in a browser message are text, not a template hole.
            () => log.Attributes[BrowserTelemetryRelay.MessageAttribute].ShouldBe("Widget {id} failed"),
            () => log.Attributes["exception.type"].ShouldBe("TypeError"),
            () => log.Attributes[BrowserTelemetryRelay.SourceAttribute].ShouldBe(BrowserTelemetryRelay.SourceValue));
    }

    [Theory]
    [InlineData(0, LogLevel.Information)]
    [InlineData(5, LogLevel.Debug)]
    [InlineData(9, LogLevel.Information)]
    [InlineData(13, LogLevel.Warning)]
    [InlineData(21, LogLevel.Critical)]
    public void LogRecord_SeverityNumber_MapsToALevel(int severityNumber, LogLevel expected)
    {
        _relay.RelayLogs(Json($$$"""
            {"resourceLogs":[{"scopeLogs":[{"logRecords":[{"severityNumber":{{{severityNumber}}},"body":{"stringValue":"x"}}]}]}]}
            """));

        _logs.Records.ShouldHaveSingleItem().Level.ShouldBe(expected);
    }

    private static string Spans(params (string Name, string TraceId, string SpanId, string ParentSpanId)[] spans)
    {
        var items = spans.Select(s => $$"""
            {"traceId":"{{s.TraceId}}","spanId":"{{s.SpanId}}","parentSpanId":"{{s.ParentSpanId}}","name":"{{s.Name}}",
             "kind":1,"startTimeUnixNano":"{{StartNanos}}","endTimeUnixNano":"{{EndNanos}}"}
            """);

        return """{"resourceSpans":[{"scopeSpans":[{"spans":[""" + string.Join(",", items) + "]}]}]}";
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
