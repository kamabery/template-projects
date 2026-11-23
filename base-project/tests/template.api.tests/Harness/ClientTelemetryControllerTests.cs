using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using template.api.Controllers;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Covers the relay the Blazor WebAssembly host reads its OTLP settings from.
/// </summary>
/// <remarks>
/// The endpoint hands a browser client the dashboard's per-run address and API key, so the
/// Development gate is the whole security story here and is worth asserting from both sides.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class ClientTelemetryControllerTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Config_RelaysTheOtlpSettingsTheAppHostSupplied()
    {
        await using var hosted = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:19114",
                    ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                    ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-otlp-api-key=secret"
                })));

        using var client = hosted.CreateClient();

        var config = await client.GetFromJsonAsync<ClientTelemetryController.ClientTelemetryConfig>(
            "/client-telemetry/config");

        config.ShouldSatisfyAllConditions(
            () => config!.Endpoint.ShouldBe("http://localhost:19114"),
            () => config!.Protocol.ShouldBe("http/protobuf"),
            () => config!.Headers.ShouldBe("x-otlp-api-key=secret"));
    }

    [Fact]
    public async Task Config_WithNothingExporting_ReturnsNulls()
    {
        // Standalone `dotnet run`: no AppHost, so no OTLP variables. The client reads this as
        // "telemetry off" rather than pointing an exporter at nothing.
        using var client = _factory.CreateClient();

        var config = await client.GetFromJsonAsync<ClientTelemetryController.ClientTelemetryConfig>(
            "/client-telemetry/config");

        config!.Endpoint.ShouldBeNull();
    }

    [Fact]
    public async Task Config_IsNotServedOutsideDevelopment()
    {
        await using var production = _factory.WithWebHostBuilder(
            builder => builder.UseEnvironment("Production"));

        using var client = production.CreateClient();

        var response = await client.GetAsync("/client-telemetry/config");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Config_InProductionWithApplicationInsights_NamesOnlyTheRelay()
    {
        // A factory preview: no dashboard to point at, so the browser posts to the API's relay,
        // and no OTLP setting is handed out.
        await using var production = _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
                    ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:19114",
                })));

        using var client = production.CreateClient();

        var config = await client.GetFromJsonAsync<ClientTelemetryController.ClientTelemetryConfig>(
            "/client-telemetry/config");

        config.ShouldSatisfyAllConditions(
            () => config!.Relay.ShouldBe("otel"),
            () => config!.Endpoint.ShouldBeNull(),
            () => config!.Headers.ShouldBeNull());
    }

    [Fact]
    public async Task Config_WithoutApplicationInsights_NamesNoRelay()
    {
        using var client = _factory.CreateClient();

        var config = await client.GetFromJsonAsync<ClientTelemetryController.ClientTelemetryConfig>(
            "/client-telemetry/config");

        config!.Relay.ShouldBeNull();
    }
}
