using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Guards the one ordering hazard in the logging setup.
/// </summary>
/// <remarks>
/// Serilog owns the pipeline outright: <c>AddSerilog</c> replaces the <see cref="ILoggerFactory"/>
/// and does not write to <see cref="ILoggerProvider"/>s, and <c>ConfigureSerilog</c> clears the
/// providers <c>ConfigureOpenTelemetry</c> registered. Both halves matter, and neither failing would
/// break another test here - a second live provider just means every record reaches the dashboard
/// twice.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class LoggingConfigurationTests : IAsyncLifetime
{
    private WeatherApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new WeatherApiFactory();
        // WebApplicationFactory builds the host lazily; CreateClient is what forces it.
        _factory.CreateClient().Dispose();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void Serilog_OwnsTheLoggerFactory() =>
        _factory.Services.GetRequiredService<ILoggerFactory>().ShouldBeOfType<SerilogLoggerFactory>();

    [Fact]
    public void NoOtherLoggerProviderSurvives() =>
        // Chiefly the OpenTelemetry one ConfigureOpenTelemetry registers, which would export the
        // same records the OTLP sink already does.
        _factory.Services.GetServices<ILoggerProvider>().ShouldBeEmpty();
}
