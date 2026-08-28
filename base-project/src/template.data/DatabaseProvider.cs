namespace template.data;

/// <summary>
/// The names that keep the AppHost's container choice, the API's EF provider choice and the
/// per-provider migrations assemblies from drifting apart.
/// </summary>
/// <remarks>
/// These live here rather than next to the configurator in template.api because the design-time
/// factory, the runtime configurator and the migrations projects all need them, and this is the one
/// assembly all three can see.
/// </remarks>
public static class DatabaseProvider
{
    /// <summary>Config key naming the provider. The AppHost reads the same key.</summary>
    public const string ConfigurationKey = "Database:Provider";

    /// <summary>Connection-string name, which is also the Aspire resource name.</summary>
    public const string ConnectionName = "weatherdb";

    public const string Postgres = "postgres";

    public const string SqlServer = "sqlserver";

    /// <summary>
    /// A local file, with no container behind it. The AppHost declares no database resource for
    /// this provider and injects the connection string directly, so `aspire run` needs no Docker.
    /// </summary>
    public const string Sqlite = "sqlite";

    public const string Default = Postgres;

    // EF discovers migrations by scanning a single assembly, and two providers' migrations cannot
    // share one: the generated SQL differs and the migration ids collide. Hence a project per
    // provider, which is Microsoft's documented answer for multiple providers.
//#if (dbProvider == "postgres")
    public const string PostgresMigrationsAssembly = "template.api.migrations.postgres";
//#endif

//#if (dbProvider == "sqlserver")
    public const string SqlServerMigrationsAssembly = "template.api.migrations.sqlserver";
//#endif

//#if (dbProvider == "sqlite")
    public const string SqliteMigrationsAssembly = "template.api.migrations.sqlite";
//#endif

    public static bool IsSqlServer(string? provider) =>
        string.Equals(provider, SqlServer, StringComparison.OrdinalIgnoreCase);

    public static bool IsSqlite(string? provider) =>
        string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase);
}
