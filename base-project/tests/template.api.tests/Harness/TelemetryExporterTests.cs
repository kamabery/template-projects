using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Which exporters <c>AddServiceDefaults</c> turns on: OTLP under the AppHost, Azure Monitor when
/// Application Insights is configured, both at once when both are.
/// </summary>
/// <remarks>
/// The services are inspected rather than the built providers: resolving a provider with a
/// connection string starts the Azure Monitor exporter, which would try to send and spill offline
/// storage onto the test machine.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class TelemetryExporterTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/";

    private const string OtlpEndpoint = "http://localhost:4317";

    [Fact]
    public void AzureMonitor_IsRegistered_WhenTheConnectionStringIsSet() =>
        HasServicesFrom(Build(("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString)), "Azure.Monitor.OpenTelemetry")
            .ShouldBeTrue();

    [Fact]
    public void AzureMonitor_IsNotRegistered_WithoutTheConnectionString() =>
        HasServicesFrom(Build(), "Azure.Monitor.OpenTelemetry").ShouldBeFalse();

    [Fact]
    public void Otlp_IsRegistered_UnderTheAppHost() =>
        HasServicesFrom(Build(("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpEndpoint)), "OpenTelemetry.Exporter.OpenTelemetryProtocol")
            .ShouldBeTrue();

    [Fact]
    public void Otlp_IsNotRegistered_OutsideTheAppHost() =>
        HasServicesFrom(Build(), "OpenTelemetry.Exporter.OpenTelemetryProtocol").ShouldBeFalse();

    [Fact]
    public void BothExporters_AreRegistered_WhenBothAreConfigured()
    {
        var builder = Build(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpEndpoint));

        HasServicesFrom(builder, "Azure.Monitor.OpenTelemetry").ShouldBeTrue();
        HasServicesFrom(builder, "OpenTelemetry.Exporter.OpenTelemetryProtocol").ShouldBeTrue();
    }

    [Fact]
    public void Sampling_KeepsEverything_ByDefault()
    {
        var options = AzureMonitorOptionsFrom(Build(("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString)));

        // The distro defaults to rate-limited sampling, which takes precedence over the ratio.
        options.TracesPerSecond.ShouldBeNull();
        options.SamplingRatio.ShouldBe(1.0F);
    }

    [Fact]
    public void Sampling_FollowsConfiguration_WhenLowered()
    {
        var options = AzureMonitorOptionsFrom(Build(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("AzureMonitor:SamplingRatio", "0.25")));

        options.TracesPerSecond.ShouldBeNull();
        options.SamplingRatio.ShouldBe(0.25F);
    }

    private static WebApplicationBuilder Build(params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });

        // The machine's own environment must not decide the answer.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = null,
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null,
        });
        builder.Configuration.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value));

        builder.AddServiceDefaults();
        return builder;
    }

    private static bool HasServicesFrom(WebApplicationBuilder builder, string assemblyPrefix) =>
        builder.Services.Any(descriptor =>
            new[]
            {
                descriptor.ServiceType,
                descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType,
                descriptor.IsKeyedService ? null : descriptor.ImplementationFactory?.Method.DeclaringType,
                descriptor.IsKeyedService ? null : descriptor.ImplementationInstance?.GetType(),
            }
            .Any(type => type?.Assembly.GetName().Name?.StartsWith(assemblyPrefix, StringComparison.Ordinal) == true));

    private static AzureMonitorOptions AzureMonitorOptionsFrom(WebApplicationBuilder builder)
    {
        using var services = builder.Services.BuildServiceProvider();
        return services.GetRequiredService<IOptionsMonitor<AzureMonitorOptions>>().Get(Options.DefaultName);
    }
}
