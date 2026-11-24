using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// In Azure the console writes compact JSON (CLEF), so the Container Apps console logs in Log
/// Analytics keep the structure; everywhere else it stays human-readable text. Its level does not
/// change: Production keeps Warning and above (see <see cref="ConsoleLevelTests"/>).
/// </summary>
/// <remarks>
/// The logging providers are removed before the container is built: resolving the OpenTelemetry
/// provider with a connection string would start the Azure Monitor exporter (see
/// <see cref="TelemetryExporterTests"/>). The console sink does not depend on them.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
[Collection(nameof(ConsoleLevelTests))]
public sealed class ConsoleFormatTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/";

    [Fact]
    public void InAzure_AConsoleLineIsCompactJson_WithTheTraceAndEntityFields()
    {
        var marker = $"json-{Guid.NewGuid():N}";

        var line = LineWith(marker, ConnectionString, logger =>
        {
            using var activity = new Activity("probe").Start();
            using (logger.ForEntity("Widget", 42))
            {
                logger.LogWarning("{Marker}", marker);
            }
        });

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        root.ShouldSatisfyAllConditions(
            () => root.GetProperty("@mt").GetString().ShouldBe("{Marker}"),
            () => root.GetProperty("trace_id").GetString()!.Length.ShouldBe(32),
            () => root.GetProperty("entity.type").GetString().ShouldBe("Widget"),
            () => root.GetProperty("entity.id").GetString().ShouldBe("42"));
    }

    [Fact]
    public void OutsideAzure_AConsoleLineIsText()
    {
        var marker = $"text-{Guid.NewGuid():N}";

        var line = LineWith(marker, connectionString: null, logger => logger.LogWarning("{Marker}", marker));

        line.ShouldStartWith("[");
        Should.Throw<JsonException>(() => JsonDocument.Parse(line));
    }

    [Fact]
    public void InAzure_ProductionStillKeepsWarningAndAbove()
    {
        var info = $"info-{Guid.NewGuid():N}";

        var output = Capture(ConnectionString, logger => logger.LogInformation("{Marker}", info));

        output.ShouldNotContain(info);
    }

    private static string LineWith(string marker, string? connectionString, Action<ILogger> log) =>
        Capture(connectionString, log)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains(marker, StringComparison.Ordinal))
            .ShouldHaveSingleItem();

    private static string Capture(string? connectionString, Action<ILogger> log)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connectionString,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null,
            });
            builder.AddServiceDefaults();
            builder.Services.RemoveAll<ILoggerProvider>();

            using (var services = builder.Services.BuildServiceProvider())
            {
                log(services.GetRequiredService<ILoggerFactory>().CreateLogger("template.api.ConsoleProbe"));
            }

            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
