using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// In Production the console keeps Warning and above only. The Container Apps environment already
/// copies console output into the Log Analytics workspace Application Insights writes to, so an
/// Information line on both would be stored twice.
/// </summary>
/// <remarks>
/// Console.Out is process-wide, so the markers are unique and only their presence is asserted;
/// another test writing to the console at the same time cannot make one of these pass or fail.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
[Collection(nameof(ConsoleLevelTests))]
public sealed class ConsoleLevelTests
{
    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public void InformationOnTheConsole_FollowsTheEnvironment(string environment, bool expected)
    {
        var info = $"info-{Guid.NewGuid():N}";
        var warning = $"warning-{Guid.NewGuid():N}";

        var output = Capture(environment, logger =>
        {
            logger.LogInformation("{Marker}", info);
            logger.LogWarning("{Marker}", warning);
        });

        output.Contains(info).ShouldBe(expected);
        output.ShouldContain(warning);
    }

    private static string Capture(string environment, Action<ILogger> log)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = null,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null,
            });
            builder.AddServiceDefaults();

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
