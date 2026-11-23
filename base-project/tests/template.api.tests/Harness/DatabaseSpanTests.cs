using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using template.api.Startup;
using template.api.tests.Infrastructure;
using template.data;

namespace template.api.tests.Harness;

/// <summary>
/// A slow query shows up as a database dependency under its request: the Aspire EF integrations
/// add their provider's activity source to the tracer that AddServiceDefaults builds.
/// </summary>
/// <remarks>
/// <para>Nothing connects. An activity is started on the provider's source by name and the test
/// checks it reached the tracing pipeline; a capturing processor, not
/// <see cref="ActivitySource.HasListeners"/>, because listeners are process-wide and another test's
/// host would answer for this one.</para>
/// <para>SQLite has no case: it gets no database spans (the EF Core instrumentation is still beta).</para>
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class DatabaseSpanTests
{
//#if (dbProvider == "postgres")
    [Fact]
    public void Postgres_CommandSpans_ReachTheTracer() =>
        Reaches(DatabaseProvider.Postgres, "Npgsql").ShouldBeTrue();
//#endif

//#if (dbProvider == "sqlserver")
    [Fact]
    public void SqlServer_CommandSpans_ReachTheTracer() =>
        Reaches(DatabaseProvider.SqlServer, "OpenTelemetry.Instrumentation.SqlClient").ShouldBeTrue();
//#endif

    private static bool Reaches(string provider, string sourceName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // No exporter: the processor below is the only reader.
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = null,
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null,
            [DatabaseProvider.ConfigurationKey] = provider,
            [$"ConnectionStrings:{DatabaseProvider.ConnectionName}"] = "Server=localhost;Database=appdb",
        });

        builder.AddServiceDefaults();
        builder.ConfigurePersistenceBuilder();

        var processor = new CapturingActivityProcessor();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(processor));

        using var services = builder.Services.BuildServiceProvider();
        _ = services.GetRequiredService<TracerProvider>();

        var name = $"probe-{Guid.NewGuid():N}";
        using (var source = new ActivitySource(sourceName))
        {
            source.StartActivity(name)?.Stop();
        }

        return processor.Activities.Any(activity => activity.OperationName == name);
    }
}
