using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Serilog.Context;
using Serilog.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Serilog is the front door for every log call, and the OpenTelemetry logging provider is the one
/// way out: to the dashboard under the AppHost, to Application Insights in Azure.
/// </summary>
/// <remarks>
/// The processor added here sits where every exporter reads from, so what it sees is what the
/// dashboard and Azure Monitor get. Levels are the API's own appsettings.json: app code at
/// Information, the framework at Warning, startup and shutdown kept.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class LoggingConfigurationTests : IAsyncLifetime
{
    private const string AppCategory = "template.api.Services.Orders";

    private readonly CapturingLogProcessor _processor = new();
    private ApiFactory _apiFactory = null!;
    private IServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _apiFactory = new ApiFactory();
        var factory = _apiFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(_processor))));

        // WebApplicationFactory builds the host lazily; CreateClient is what forces it.
        factory.CreateClient().Dispose();
        _services = factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _apiFactory.DisposeAsync();

    [Fact]
    public void Serilog_OwnsTheLoggerFactory() =>
        _services.GetRequiredService<ILoggerFactory>().ShouldBeOfType<SerilogLoggerFactory>();

    [Fact]
    public void TheOpenTelemetryProvider_IsTheOnlyWayOut() =>
        // One provider, so nothing reaches an exporter twice - Serilog's own OTLP sink is gone.
        _services.GetServices<ILoggerProvider>().ShouldHaveSingleItem().ShouldBeOfType<OpenTelemetryLoggerProvider>();

    [Fact]
    public void AnAppLog_ReachesOpenTelemetry_WithItsPropertiesAndItsTrace()
    {
        using var activity = new Activity("save").Start();

        using (LogContext.PushProperty("Tenant", "north"))
        {
            Logger(AppCategory).LogInformation("Saved widget {WidgetId}", 42);
        }

        var record = Single(AppCategory);
        record.Level.ShouldBe(LogLevel.Information);
        record.TraceId.ShouldBe(activity.TraceId.ToHexString());
        record.Attributes["WidgetId"].ShouldBe(42);
        record.Attributes["Tenant"].ShouldBe("north");
    }

    [Fact]
    public void ANestedObject_ArrivesAsJson()
    {
        Logger(AppCategory).LogInformation("Saved {@Order}", new { Id = 7, Lines = new[] { "a", "b" } });

        var json = Single(AppCategory).Attributes["Order"].ShouldBeOfType<string>();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("Id").GetInt32().ShouldBe(7);
        document.RootElement.GetProperty("Lines").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void AppDebug_IsNotTracked()
    {
        Logger(AppCategory).LogDebug("Too chatty");

        _processor.Records.ShouldNotContain(r => r.Category == AppCategory);
    }

    [Fact]
    public void FrameworkInformation_IsNotTracked()
    {
        // EF Core logs every SQL command at Information; the database dependency span covers it.
        const string category = "Microsoft.EntityFrameworkCore.Database.Command";
        Logger(category).LogInformation("Executed DbCommand");

        _processor.Records.ShouldNotContain(r => r.Category == category);
    }

    [Fact]
    public void StartupAndShutdown_AreTracked()
    {
        // The host's own startup lines arrive under this category too, so look for this one.
        const string category = "Microsoft.Hosting.Lifetime";
        var marker = Guid.NewGuid().ToString("N");
        Logger(category).LogInformation("Application started {Marker}", marker);

        _processor.Records.ShouldContain(r =>
            r.Category == category && r.Level == LogLevel.Information && Equals(r.Attributes.GetValueOrDefault("Marker"), marker));
    }

    private ILogger Logger(string category) =>
        _services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

    private CapturedLog Single(string category) =>
        _processor.Records.Where(r => r.Category == category).ShouldHaveSingleItem();
}
