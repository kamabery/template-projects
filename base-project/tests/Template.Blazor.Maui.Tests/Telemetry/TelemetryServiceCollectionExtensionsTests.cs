using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Telemetry;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class TelemetryServiceCollectionExtensionsTests
{
    private static ClientTelemetryOptions Enabled => new()
    {
        ServiceName = "blazor-web",
        Endpoint = "http://localhost:19114",
        Protocol = "http/protobuf",
        Headers = "x-otlp-api-key=secret"
    };

    private static ClientTelemetryOptions Disabled => new() { ServiceName = "blazor-web" };

    [Fact]
    public void AddSharedTelemetry_WithAnEndpoint_RegistersBothProviders()
    {
        using var provider = Build(Enabled);

        provider.ShouldSatisfyAllConditions(
            () => provider.GetService<TracerProvider>().ShouldNotBeNull(),
            () => provider.GetService<MeterProvider>().ShouldNotBeNull());
    }

    [Fact]
    public void AddSharedTelemetry_OnASingleThreadedRuntime_RegistersNoMeterProvider()
    {
        // The WebAssembly host. Its metric reader collects on a thread the browser never runs, so a
        // MeterProvider there would be wiring that looks live and is not. Traces still work, because
        // they are switched to the simple export processor instead.
        using var provider = Build(Enabled with { IsSingleThreadedRuntime = true });

        provider.ShouldSatisfyAllConditions(
            () => provider.GetService<TracerProvider>().ShouldNotBeNull(),
            () => provider.GetService<MeterProvider>().ShouldBeNull());
    }

    [Fact]
    public void AddSharedTelemetry_WithoutAnEndpoint_RegistersNoProviders()
    {
        // The no-op path: `dotnet run` outside the AppHost, or an API that relayed nothing. Building
        // exporters pointed at no endpoint is the failure this gate exists to avoid.
        using var provider = Build(Disabled);

        provider.ShouldSatisfyAllConditions(
            () => provider.GetService<TracerProvider>().ShouldBeNull(),
            () => provider.GetService<MeterProvider>().ShouldBeNull());
    }

    [Fact]
    public void AddSharedTelemetry_WithARelay_RegistersTracesButNoMetrics()
    {
        // The WebAssembly host in Azure. The relay takes traces and logs only.
        using var provider = Build(Disabled with
        {
            RelayEndpoint = new Uri("https://api.example.test/otel"),
            IsSingleThreadedRuntime = true,
        });

        provider.ShouldSatisfyAllConditions(
            () => provider.GetService<TracerProvider>().ShouldNotBeNull(),
            () => provider.GetService<MeterProvider>().ShouldBeNull());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddSharedTelemetry_RegistersNavigationTelemetryEitherWay(bool enabled)
    {
        // MainLayout injects it unconditionally, so gating this registration on the endpoint would
        // turn a disabled exporter into a missing-service exception on every standalone run.
        using var provider = Build(enabled ? Enabled : Disabled);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<NavigationTelemetry>().ShouldNotBeNull();
    }

    private static ServiceProvider Build(ClientTelemetryOptions options)
    {
        var services = new ServiceCollection();

        // NavigationTelemetry's only dependency. bUnit supplies a real one to a rendered component;
        // here the registration itself is what is under test.
        services.AddSingleton<Microsoft.AspNetCore.Components.NavigationManager, StubNavigationManager>();
        services.AddSharedTelemetry(options);

        return services.BuildServiceProvider();
    }
}
