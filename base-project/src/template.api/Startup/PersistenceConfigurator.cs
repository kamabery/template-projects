using Microsoft.EntityFrameworkCore;
using template.data;
using template.data.Repositories;

namespace template.api.Startup;

/// <summary>
/// Wires the DbContext, the generic repository and the startup-time schema/seed step.
/// </summary>
/// <remarks>
/// <para>Three providers are wired rather than one. The AppHost reads the same
/// <see cref="DatabaseProvider.ConfigurationKey"/> value to decide which container to start and
/// forwards it here as Database__Provider, so the container and the EF provider cannot disagree.
/// Every branch compiles and every branch runs here; the dbProvider scaffolding parameter
/// collapses this to whichever one was asked for and prunes the other migrations projects with
/// it.</para>
/// <para>The Aspire client integrations are used rather than a plain AddDbContext. They resolve the
/// connection string the AppHost injects under ConnectionStrings:weatherdb, and they also register
/// an EF health check (so the api resource reports unhealthy when the database is down rather than
/// throwing on the first request), EF and ADO OpenTelemetry spans that appear under the api's
/// requests in the dashboard's trace view, and retry-on-transient-failure.</para>
/// </remarks>
public static class PersistenceConfigurator
{
    public static IHostApplicationBuilder ConfigurePersistenceBuilder(this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration[DatabaseProvider.ConfigurationKey] ?? DatabaseProvider.Default;

        // Each provider is its own conditional region rather than one if/else chain. The regions are
        // mutually exclusive at run time, so this workspace behaves exactly as a chain would; a
        // scaffold keeps only the provider it asked for and the other regions are removed outright.
//#if (dbProvider == "sqlite")
        if (DatabaseProvider.IsSqlite(provider))
        {
            // No Aspire client integration here, unlike the two branches below. SQLite is a local
            // file rather than a service, so there is no container for the AppHost to start and
            // nothing to inject a connection string for - the AppHost sets ConnectionStrings__weatherdb
            // directly. AddDbContextPool, not AddDbContext, to match the lifetime the Aspire
            // integrations register on the other two branches, so the test suite's override behaves
            // the same way whichever provider was scaffolded.
            //
            // The consequence worth knowing: this branch registers no EF health check, so the api
            // resource's /health does not cover the database. That is the honest reading - a missing
            // file is a startup failure, not a dependency outage - and adding one would mean taking
            // a new package for a provider that cannot be unreachable.
            builder.Services.AddDbContextPool<WeatherDbContext>(options => options.UseSqlite(
                builder.Configuration.GetConnectionString(DatabaseProvider.ConnectionName)
                    ?? "Data Source=weather.db",
                sqlite => sqlite.MigrationsAssembly(DatabaseProvider.SqliteMigrationsAssembly)));
        }
//#endif

//#if (dbProvider == "sqlserver")
        if (DatabaseProvider.IsSqlServer(provider))
        {
            builder.AddSqlServerDbContext<WeatherDbContext>(
                DatabaseProvider.ConnectionName,
                configureDbContextOptions: options => options.UseSqlServer(
                    sql => sql.MigrationsAssembly(DatabaseProvider.SqlServerMigrationsAssembly)));
        }
//#endif

//#if (dbProvider == "postgres")
        if (!DatabaseProvider.IsSqlite(provider) && !DatabaseProvider.IsSqlServer(provider))
        {
            builder.AddNpgsqlDbContext<WeatherDbContext>(
                DatabaseProvider.ConnectionName,
                configureDbContextOptions: options => options.UseNpgsql(
                    npgsql => npgsql.MigrationsAssembly(DatabaseProvider.PostgresMigrationsAssembly)));
        }
//#endif

        // Open-generic, so IRepository<Thing> resolves for any entity without a line per entity.
        builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        return builder;
    }

    /// <summary>
    /// Brings the schema up to date and seeds it. Development only.
    /// </summary>
    /// <remarks>
    /// <para>Migrating on startup is right for a scaffold and wrong for production - concurrent
    /// instances would race, and a schema change would deploy itself unreviewed. Hence the
    /// Development gate; a real deployment runs `dotnet ef database update` from its pipeline.</para>
    /// <para>The GetMigrations branch is load-bearing, not defensive. Migrations live in the
    /// provider-specific assemblies named on <see cref="DatabaseProvider"/>, so a context configured
    /// for a provider that has none finds none - which is exactly what the test suite's in-memory
    /// SQLite override lands on, and is what lets `dotnet test` keep working with no Docker.
    /// Building the schema from the model is the correct behaviour in that case, so this needs no
    /// test-awareness to express.</para>
    /// </remarks>
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        await WeatherSeeder.SeedAsync(context);

        return app;
    }
}
