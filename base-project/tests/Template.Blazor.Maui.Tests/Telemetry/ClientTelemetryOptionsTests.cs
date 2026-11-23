using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Telemetry;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class ClientTelemetryOptionsTests
{
    [Fact]
    public void ParseKeyValueList_ReadsTheBaggageShapeAndDecodesValues()
    {
        var parsed = ClientTelemetryOptions
            .ParseKeyValueList("x-otlp-api-key=a%20b, service.instance.id=one")
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        parsed.ShouldSatisfyAllConditions(
            () => parsed["x-otlp-api-key"].ShouldBe("a b"),
            () => parsed["service.instance.id"].ShouldBe("one"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator-here")]
    public void ParseKeyValueList_OnNothingUsable_YieldsNothing(string? raw) =>
        ClientTelemetryOptions.ParseKeyValueList(raw).ShouldBeEmpty();

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("http://localhost:19114", true)]
    public void IsEnabled_FollowsTheEndpoint(string? endpoint, bool expected) =>
        new ClientTelemetryOptions { ServiceName = "test", Endpoint = endpoint }.IsEnabled.ShouldBe(expected);

    [Theory]
    [InlineData("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("grpc", OtlpExportProtocol.Grpc)]
    // Absent means gRPC, which is what the .NET OpenTelemetry SDK defaults to.
    [InlineData(null, OtlpExportProtocol.Grpc)]
    public void ExportProtocol_MapsTheEnvironmentValue(string? protocol, OtlpExportProtocol expected) =>
        new ClientTelemetryOptions { ServiceName = "test", Protocol = protocol }.ExportProtocol.ShouldBe(expected);

    [Fact]
    public void FromConfiguration_ReadsTheStandardOtlpVariables()
    {
        var options = ClientTelemetryOptions.FromConfiguration(
            Configuration(new()
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:19114",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-otlp-api-key=secret",
                ["OTEL_SERVICE_NAME"] = "maui"
            }),
            "fallback");

        options.ShouldSatisfyAllConditions(
            () => options.Endpoint.ShouldBe("http://localhost:19114"),
            () => options.Headers.ShouldBe("x-otlp-api-key=secret"),
            // The AppHost's resource name wins, so the dashboard files this host under it.
            () => options.ServiceName.ShouldBe("maui"),
            () => options.IsEnabled.ShouldBeTrue());
    }

    [Fact]
    public void FromConfiguration_OutsideTheAppHost_IsDisabledAndFallsBackToTheGivenName()
    {
        var options = ClientTelemetryOptions.FromConfiguration(Configuration([]), "maui");

        options.ShouldSatisfyAllConditions(
            () => options.IsEnabled.ShouldBeFalse(),
            () => options.ServiceName.ShouldBe("maui"));
    }

    [Fact]
    public async Task FetchAsync_WhenTheApiNamesItsRelay_PointsAtIt()
    {
        // An Azure deployment: no dashboard, Application Insights behind the API's relay.
        using var client = Api("""{"endpoint":null,"protocol":null,"headers":null,"relay":"otel"}""");

        var options = await ClientTelemetryOptions.FetchAsync(client, "blazor-web");

        options.ShouldSatisfyAllConditions(
            () => options.RelayEndpoint.ShouldBe(new Uri("https://api.example.test/otel")),
            () => options.Endpoint.ShouldBeNull(),
            () => options.IsEnabled.ShouldBeTrue());
    }

    [Fact]
    public async Task FetchAsync_WithADashboard_PrefersItToTheRelay()
    {
        using var client = Api(
            """{"endpoint":"http://localhost:19114","protocol":"http/protobuf","headers":"x-otlp-api-key=k","relay":"otel"}""");

        var options = await ClientTelemetryOptions.FetchAsync(client, "blazor-web");

        options.ShouldSatisfyAllConditions(
            () => options.Endpoint.ShouldBe("http://localhost:19114"),
            () => options.RelayEndpoint.ShouldBeNull());
    }

    [Fact]
    public async Task FetchAsync_WithNeither_IsDisabled()
    {
        using var client = Api("""{"endpoint":null,"protocol":null,"headers":null,"relay":null}""");

        (await ClientTelemetryOptions.FetchAsync(client, "blazor-web")).IsEnabled.ShouldBeFalse();
    }

    private static HttpClient Api(string configJson) =>
        new(new CapturingHttpHandler(configJson)) { BaseAddress = new Uri("https://api.example.test/") };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
