#:sdk Aspire.AppHost.Sdk@13.5.1
//#if (includeReact)
#:package Aspire.Hosting.JavaScript@13.5.1
//#endif
//#if (dbProvider == "postgres" || includeEventsMarten)
#:package Aspire.Hosting.PostgreSQL@13.5.1
//#endif
//#if (dbProvider == "sqlserver" || includeEventsPolecat)
#:package Aspire.Hosting.SqlServer@13.5.1
//#endif
//#if (includeEventsPolecat)
// Only the Polecat events host can ask for a broker, and only when Events:Transport says so. The
// package is still needed unconditionally there, because both transport branches are live code.
#:package Aspire.Hosting.Azure.ServiceBus@13.5.1
//#endif
// <<addon-packages>> - scaffolding inserts add-on #:package directives above this line.
#:property AspireUseCliBundle=true

// AddCSharpApp rather than AddProject<T>. A file-based AppHost has no .csproj to hang
// ProjectReference items off, so there is no generated Projects namespace to bind to - and for the
// MAUI host that is the feature, not the limitation. An AppHost that referenced it would have to be
// reference-compatible with net10.0-windows10.0.19041.0 and would pull the MAUI workload into every
// `aspire run`. AddCSharpApp takes a path and needs no reference at all. It still returns a
// ProjectResource, so WithReference / WithHttpHealthCheck / PublishAsStaticWebsite all behave.
#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

// Paths are relative to this file's directory, not the repository root.

// The forecast lives in a store rather than in the API's memory. Every provider is wired here, and
// the same Database:Provider value is forwarded to the API below as Database__Provider, so the
// store that starts and the EF provider that talks to it cannot disagree. The dbProvider
// scaffolding parameter collapses this to whichever single branch was asked for.
//
// WithDataVolume on the container branches: rows survive `aspire stop`, which is why WidgetSeeder
// checks staleness rather than emptiness. Drop the volume to start clean.
var databaseProvider = builder.Configuration["Database:Provider"] ?? "postgres";

// One nullable slot, filled in by whichever provider branch is live. sqlite leaves it null on
// purpose: it is a local file, not a service, so there is no container to declare and nothing to
// wait for - which is what lets `aspire run` come up on a machine with no Docker at all.
//
// Each branch carries both a runtime guard and a scaffolding conditional. In this workspace all of
// them are present and Database:Provider picks between them at run time; in a scaffold only the
// provider that was asked for survives, and the guard it keeps is simply always true.
IResourceBuilder<IResourceWithConnectionString>? appdb = null;

//#if (dbProvider == "postgres")
if (databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    appdb = builder.AddPostgres("pg")
                       .WithDataVolume()
                       .AddDatabase("appdb");
}
//#endif

//#if (dbProvider == "sqlserver" || includeEventsPolecat)
// A second slot, holding the SQL Server itself rather than a database on it. The events slice adds
// its event store as another database on this same server, so it needs the server and appdb
// above is only a database. Nothing else uses it, and the Postgres and SQLite branches leave it
// null.
IResourceBuilder<SqlServerServerResource>? sqlServer = null;
//#endif

//#if (dbProvider == "sqlserver")
if (databaseProvider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
{
    // The image is Developer edition by default; MSSQL_PID picks Express, which is what the
    // licence-free path for a scaffolded project looks like.
    //
    // 2025-latest rather than the default 2022, and pinned rather than floating: Polecat stores
    // events and documents in SQL Server 2025's native json column type and generates JSON_OBJECT
    // and GENERATE_SERIES, none of which exist in 2022. The API itself does not care, but the two
    // share this server, so the version has to satisfy the stricter of them. Azure SQL Database has
    // the native json type GA, so the deployed path needs nothing extra.
    sqlServer = builder.AddSqlServer("sql")
                       .WithImageTag("2025-latest")
                       .WithEnvironment("MSSQL_PID", "Express")
                       .WithDataVolume();

    appdb = sqlServer.AddDatabase("appdb");
}
//#endif

// The API picks up the OTEL_* environment variables the AppHost injects and wires them up in
// AddServiceDefaults() (see template.servicedefaults).
//
// The "http" profile is pinned rather than left to Aspire's default, for two reasons. Both Blazor
// hosts read a hard-coded ApiBaseAddress (wwwroot/appsettings.json for WASM, the embedded
// appsettings.json for MAUI), so the API's port has to be deterministic. And serving the WASM page
// over https while that address stays http is a mixed-content block in the browser.
var api = builder.AddCSharpApp("api", "../base-project/src/template.api/template.api.csproj",
                               options => options.LaunchProfileName = "http")
    .WithEnvironment("Database__Provider", databaseProvider)
    // HttpProtobuf rather than the default gRPC, for the browser's sake rather than the API's. The
    // Blazor WebAssembly host cannot read this AppHost's environment and cannot commit the
    // dashboard's per-run API key, so it asks the API for the OTLP settings instead
    // (/client-telemetry/config, Development only). A browser cannot speak gRPC, so the endpoint the
    // API hands out has to be one it can post to - and the simplest way to guarantee that is for the
    // API to be using it itself.
    .WithOtlpExporter(OtlpProtocol.HttpProtobuf)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    // Point the dashboard's link for this resource at the Swagger UI page rather than the API
    // root, which 404s - nothing is routed at "/". WithUrlForEndpoint rewrites the endpoint's
    // existing URL instead of adding a second one, and the relative path is resolved against the
    // endpoint, so this survives a port change even though the http profile is pinned above.
    // Resource URLs are a run-mode affordance; Aspire ignores them on publish.
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "API docs";
        url.Url = "/api-docs";
    });

if (appdb is not null)
{
    // WithReference here, unlike blazor-web below, because the API is a .NET process that reads
    // ConnectionStrings:appdb straight out of its configuration. WaitFor is not optional: the
    // API migrates and seeds at startup, so racing the container throws rather than retrying.
    api.WithReference(appdb).WaitFor(appdb);
}
else
{
    // The sqlite branch. Same configuration key the container branches populate via WithReference,
    // set by hand because there is no resource to reference. Relative to the API's working
    // directory, so the file lands beside the built API rather than anywhere absolute.
    api.WithEnvironment("ConnectionStrings__appdb", "Data Source=app.db");
}

// <<addon-resources>> - scaffolding inserts add-on resource declarations below this line.

//#if (includeReact)
// React, on the Vite dev server.
//   WithReference(api)  -> API_HTTP / API_HTTPS, read by vite.config.ts for the /api proxy.
//   WithOtlpExporter    -> OTLP endpoint + api key for browser RUM. HttpProtobuf is required:
//                          browsers cannot speak the gRPC protocol the .NET services use.
var ui = builder.AddViteApp("ui", "../base-project/src/ui/react")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithOtlpExporter(OtlpProtocol.HttpProtobuf)
    .WithExternalHttpEndpoints();
//#endif

//#if (includeBlazorWeb)
// Blazor WebAssembly. WaitFor, not WithReference: WithReference injects service-discovery
// environment variables and this client cannot read them - the browser fetches its configuration
// from wwwroot/appsettings.json. WaitFor still gates startup on the health check above, which is
// the half of WithReference that actually buys something here. Because it is a real cross-origin
// browser client, it is also the only one of the three that needs CORS on the API.
builder.AddCSharpApp("blazor-web",
                     "../base-project/src/ui/blazor/Template.Blazor.Maui.Web/Template.Blazor.Maui.Web.csproj",
                     options => options.LaunchProfileName = "http")
    .WaitFor(api)
    .WithExternalHttpEndpoints();
//#endif

//#if (includeMaui)
// The MAUI host is a desktop client, not a service, and it only builds where its workload and
// target framework line up - net10.0-windows10.0.19041.0 on Windows, ios/maccatalyst on a Mac.
// WithExplicitStart keeps it out of the automatic startup set: it appears in the dashboard as
// "Not started" with a Start button, so `aspire run` still comes up green on a box that has no
// MAUI workload installed.
builder.AddCSharpApp("maui", "../base-project/src/ui/blazor/Template.Blazor.Maui/Template.Blazor.Maui.csproj")
    .WithExplicitStart();
//#endif

//#if (includeEventsMarten || includeEventsPolecat || includeEventsFisher)
// The optional event-sourcing slice, behind Events:Enabled. Read the same way Database:Provider is
// above, and with the same intent: one configuration value decides the shape of the run, in one
// place.
//
// Which event store runs is deliberately NOT a second choice - it follows Database:Provider.
// Marten on Postgres, Polecat on SQL Server, Fisher on SQLite: the same slice, the same handler
// code, three assemblies, because each of those libraries declares its own IDocumentSession,
// SingleStreamProjection and [AggregateHandler]. Each branch below therefore guards on the
// provider as well as the flag, so exactly one of them can declare the "events" resource in this
// workspace; a scaffold ships only the host matching the provider it asked for.
//
// The flag gates the AppHost only. Nothing inside the events service reads it - when the flag is
// off neither the service nor its store is declared, so there is nothing running to gate. That is
// why this is a plain configuration read rather than Microsoft.FeatureManagement.
// bool.TryParse rather than Configuration.GetValue<bool>: GetValue is an extension method from
// Microsoft.Extensions.Configuration.Binder, and a file-based AppHost has only what its `#:package`
// directives pull in - which does not include it. The raw indexer is also how Database:Provider is
// read above, so this stays consistent with it.
var eventsEnabled = bool.TryParse(builder.Configuration["Events:Enabled"], out var eventsFlag) && eventsFlag;
//#endif

//#if (includeEventsMarten)
if (eventsEnabled && databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    // Its own container, unlike the SQL Server branch below, even though Marten and the API's store
    // are the same engine here. This is the proven path and leaving it exactly as it was is worth
    // more than symmetry with that branch; the two stores also stay independent this way.
    var eventstore = builder.AddPostgres("events-pg")
                            .WithDataVolume()
                            .AddDatabase("eventstore");

    builder.AddCSharpApp("events", "../event-project/src/template.events/template.events.csproj",
                         options => options.LaunchProfileName = "http")
        // WithReference, like api above and unlike blazor-web: this is a .NET process that reads
        // ConnectionStrings:eventstore straight out of its configuration. WaitFor matters because
        // Marten applies its schema at startup in Development.
        .WithReference(eventstore)
        .WaitFor(eventstore)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });
}
//#endif

//#if (includeEventsPolecat)
if (eventsEnabled
    && databaseProvider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase)
    && sqlServer is not null)
{
    // A second database on the API's SQL Server rather than a container of its own. Polecat is
    // Marten's API on SQL Server, so unlike the Postgres branch there is no engine mismatch to work
    // around - the two stores are already isolated by database, and a second SQL Server container
    // plus the Service Bus emulator's own companion would be three engines on one machine.
    var eventstore = sqlServer.AddDatabase("eventstore");

    // "local" by default: Wolverine's durable local queues are backed by the same SQL Server outbox
    // tables, so the whole outbox path is exercised with no broker and no extra container.
    // "azureservicebus" is the Azure-shaped path and costs two more containers.
    var eventsTransport = builder.Configuration["Events:Transport"] ?? "local";

    var events = builder.AddCSharpApp("events",
                                      "../event-project/src/template.events.polecat/template.events.polecat.csproj",
                                      options => options.LaunchProfileName = "http")
        .WithReference(eventstore)
        .WaitFor(eventstore)
        // Forwarded so the service and this file cannot disagree about the transport, exactly as
        // Database__Provider is forwarded to the API above.
        .WithEnvironment("Events__Transport", eventsTransport)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });

    if (eventsTransport.Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
    {
        // The emulator, so this runs with no Azure subscription. It is not free: the image requires
        // accepting a EULA and starts its own companion SQL Server, which is why this is opt-in
        // rather than the default for the sqlserver branch.
        //
        // The AMQP port is pinned because Wolverine's UseAzureServiceBusEmulator() addresses the
        // emulator at its well-known ports rather than through service discovery. It also needs the
        // management endpoint on 5300; if that turns out not to be reachable, the fallback is to
        // drop AutoProvision() in the events service and rely on the queue declared just below,
        // which Aspire writes into the emulator's own configuration.
        var serviceBus = builder.AddAzureServiceBus("servicebus")
            .RunAsEmulator(emulator => emulator.WithHostPort(5672));

        serviceBus.AddServiceBusQueue("widget-completed");

        events.WithReference(serviceBus).WaitFor(serviceBus);
    }
}
//#endif

//#if (includeEventsFisher)
if (eventsEnabled && databaseProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    // No store resource at all, which is the entire point of this branch: choosing SQLite means
    // `aspire run` needs no Docker, and turning events on must not quietly take that away. The
    // connection string is set by hand for the same reason the API's is above - there is no
    // resource to reference - and is relative, so the file lands beside the built service.
    builder.AddCSharpApp("events",
                         "../event-project/src/template.events.fisher/template.events.fisher.csproj",
                         options => options.LaunchProfileName = "http")
        .WithEnvironment("ConnectionStrings__eventstore", "Data Source=eventstore.db")
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });
}
//#endif

//#if (includeBlazorWeb || includeMaui)
// Template.Blazor.Maui.Pages is deliberately absent. It is a Razor Class Library with no entry
// point, so there is no process for Aspire to launch; it is built as a dependency of the
// Blazor resources above.
//#endif

// <<addon-publish>> - scaffolding inserts add-on publish-time configuration below this line.

//#if (includeReact)
// On publish the React UI becomes a static site behind YARP, which also reverse-proxies /api to
// the API so the browser stays same-origin exactly as it does in dev.
#pragma warning disable ASPIREJAVASCRIPT001
ui.PublishAsStaticWebsite("/api", api);
#pragma warning restore ASPIREJAVASCRIPT001
//#endif

builder.Build().Run();
