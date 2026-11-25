using System.Diagnostics;
using Microsoft.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Every record carries the W3C trace context as named fields - <c>trace_id</c>, <c>span_id</c>,
/// <c>trace_flags</c> and <c>parent_span_id</c> - so the console and the dashboard can join a log to
/// its trace, not only Application Insights through <c>operation_Id</c>.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class TraceContextLoggingTests : IAsyncLifetime
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string CallerSpanId = "00f067aa0ba902b7";

    private ObservedApi _api = null!;

    public Task InitializeAsync()
    {
        _api = new ObservedApi();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ALogInsideARequest_CarriesTheRequestsTraceContext()
    {
        var marker = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/probe-log/{marker}");
        request.Headers.Add("traceparent", $"00-{TraceId}-{CallerSpanId}-01");

        await _api.Client.SendAsync(request);

        var record = _api.LogWith("Marker", marker);
        var span = await _api.RequestSpanAsync(TraceId);
        record.ShouldSatisfyAllConditions(
            () => record.Attributes["trace_id"].ShouldBe(TraceId),
            () => record.Attributes["span_id"].ShouldBe(span.SpanId.ToHexString()),
            () => record.Attributes["trace_flags"].ShouldBe("01"),
            () => record.Attributes["parent_span_id"].ShouldBe(CallerSpanId));
    }

    [Fact]
    public async Task ARequestWithNoCaller_HasNoParentSpanId()
    {
        var marker = Guid.NewGuid().ToString("N");

        await _api.Client.GetAsync($"/probe-log/{marker}");

        var record = _api.LogWith("Marker", marker);
        record.Attributes["trace_id"].ShouldBe(record.TraceId);
        record.Attributes.ShouldNotContainKey("parent_span_id");
    }

    [Fact]
    public void ALogWithNoActivity_HasNoTraceFields()
    {
        var marker = Guid.NewGuid().ToString("N");
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            _api.Logger("template.api.TraceProbe").LogInformation("Probe {Marker}", marker);
        }
        finally
        {
            Activity.Current = previous;
        }

        var attributes = _api.LogWith("Marker", marker).Attributes;
        attributes.Keys.ShouldNotContain(key => key == "trace_id" || key == "span_id" || key == "trace_flags" || key == "parent_span_id");
    }
}
