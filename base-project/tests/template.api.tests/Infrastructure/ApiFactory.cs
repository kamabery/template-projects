using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using template.data;

namespace template.api.tests.Infrastructure;

/// <summary>
/// Hosts the real template.api against an in-memory SQLite database, so the API suite still runs
/// with no Docker.
/// </summary>
/// <remarks>
/// <para>Two overrides are needed and the order matters. The Aspire client integration throws at
/// registration when ConnectionStrings:appdb is absent - before ConfigureTestServices ever
/// runs - so a placeholder has to be in configuration first. Nothing ever connects to it; the
/// DbContext registration is replaced below.</para>
/// <para>The connection is held open for the lifetime of the factory on purpose: an in-memory
/// SQLite database exists only while at least one connection to it is open, so letting EF open and
/// close per operation would discard the schema and the seed between requests.</para>
/// <para>Nothing here tells the application it is under test. The app's startup step applies
/// migrations when the configured provider has them and builds the schema from the model when it
/// does not; the migrations live in provider-specific assemblies, so a SQLite context finds none
/// and lands on the second path by itself. See PersistenceConfigurator.InitializeDatabaseAsync.</para>
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory() => _connection.Open();

    /// <summary>
    /// Runs <paramref name="action"/> against the same database the API is serving from.
    /// </summary>
    public async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();

        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: this has to land in host configuration, which
        // WebApplication.CreateBuilder has already read by the time ConfigureAppConfiguration
        // callbacks are applied. Placeholder only - the registration below replaces the context, so
        // nothing ever opens a connection with it, but the Aspire integration validates that the
        // string is present before it will register at all.
        builder.UseSetting("ConnectionStrings:appdb", "Host=localhost;Database=appdb");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            // The one that actually matters. Since EF Core 9 the provider is applied through
            // IDbContextOptionsConfiguration<T> descriptors and *every* registered one runs, so
            // leaving Aspire's in place stacks Npgsql onto the same options object as SQLite and
            // EF refuses to build: "Only a single database provider can be registered."
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            // Aspire also registers Npgsql's data source and EF services in the application
            // container; clearing them lets EF build its internal provider from the options below.
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<DbConnection>();

            // AddDbContextPool, not AddDbContext, to match what the Aspire integration did. It
            // registers DbContextOptions as a singleton and leaves a singleton IDbContextPool
            // behind that this cannot remove - it is an internal type - so a scoped AddDbContext
            // here fails DI validation with "cannot consume scoped service from singleton".
            services.AddDbContextPool<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
