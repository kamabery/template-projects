using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// The relay is anonymous, so the guard is what keeps it a browser's telemetry path and not an
/// open write into Application Insights: same-origin or an allowed origin, 64 KB, traces and logs
/// only, a rate limit per client.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class OtlpRelayGuardTests : IAsyncLifetime
{
    private const string AllowedOrigin = "https://wasm.example.test";
    private const string TracesPath = "/otel/v1/traces";
    private const string EmptyTraces = """{"resourceSpans":[]}""";

    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SameOriginTraces_AreAccepted()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post(TracesPath, EmptyTraces, SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SameOriginLogs_AreAccepted()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post("/otel/v1/logs", """{"resourceLogs":[]}""", SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnAllowedOrigin_IsAccepted()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post(TracesPath, EmptyTraces, request =>
        {
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
            request.Headers.Add("Origin", AllowedOrigin);
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ACrossOriginRequest_IsRefused()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post(TracesPath, EmptyTraces, request =>
        {
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
            request.Headers.Add("Origin", "https://elsewhere.example.test");
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ARequestWithNoBrowserHeaders_IsRefused()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post(TracesPath, EmptyTraces, _ => { }));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ABodyOver64KB_IsRefused()
    {
        using var client = Configured().CreateClient();
        var oversized = $$"""{"resourceSpans":[],"padding":"{{new string('x', 64 * 1024)}}"}""";

        var response = await client.SendAsync(Post(TracesPath, oversized, SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task AMetricsPayload_IsRefused()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post("/otel/v1/metrics", """{"resourceMetrics":[]}""", SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Protobuf_IsRefused()
    {
        using var client = Configured().CreateClient();
        var request = Post(TracesPath, EmptyTraces, SameOrigin);
        request.Content!.Headers.ContentType = new("application/x-protobuf");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task MalformedJson_IsABadRequest()
    {
        using var client = Configured().CreateClient();

        var response = await client.SendAsync(Post(TracesPath, "{not json", SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ABurstOverTheRateLimit_IsRefused_ForThatClientOnly()
    {
        using var client = Configured().CreateClient();

        HttpStatusCode last = default;
        for (var i = 0; i <= 120; i++)
        {
            last = (await client.SendAsync(Post(TracesPath, EmptyTraces, From("203.0.113.7")))).StatusCode;
        }

        var other = await client.SendAsync(Post(TracesPath, EmptyTraces, From("198.51.100.9")));

        last.ShouldBe(HttpStatusCode.TooManyRequests);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithoutApplicationInsights_TheRelayIsNotThere()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(Post(TracesPath, EmptyTraces, SameOrigin));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private WebApplicationFactory<Program> Configured() =>
        _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Nothing is sent: the relay only checks it is set, and the exporter it turns on
                    // is not started by these requests' spans being dropped at the test host.
                    ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/",
                    ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                })));

    private static void SameOrigin(HttpRequestMessage request) =>
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

    private static Action<HttpRequestMessage> From(string clientIp) => request =>
    {
        SameOrigin(request);
        request.Headers.Add("X-Forwarded-For", $"{clientIp}, 10.0.0.1");
    };

    private static HttpRequestMessage Post(string path, string json, Action<HttpRequestMessage> headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        headers(request);
        return request;
    }
}
