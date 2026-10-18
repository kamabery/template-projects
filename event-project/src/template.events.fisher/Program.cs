using Fisher;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using template.events.Abstractions;
using template.events.Messaging;
using template.events.Projections;
using template.events.Storage;
using Wolverine;
using Wolverine.Fisher;

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks and OpenTelemetry - the same stock Aspire file
// template.api uses. It is what supplies /health, which the AppHost gates this resource on, and
// what puts the Fisher and Wolverine spans under the incoming request in the dashboard's traces.
builder.AddServiceDefaults();

// WidgetsController comes from src/shared/, but that is linked source rather than a referenced
// assembly, so it compiles into this one and AddControllers finds it by scanning the entry assembly
// as usual. No AddApplicationPart needed - which is a second, smaller benefit of linking.
builder.Services.AddControllers();

builder.Services.AddOpenApi();

// The one store-specific thing the shared controller needs. See IWidgetSummaries for why this seam
// exists rather than the controller taking IQuerySession directly.
builder.Services.AddScoped<IWidgetSummaries, WidgetSummaries>();

// Injected rather than called statically so the handlers stay pure functions the unit tests can
// call with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// The event store. This host is the SQLite one, and it is the reason the sqlite branch can offer
// events at all: a local file, no container, no Docker. The AppHost declares no resource for it and
// injects the file path directly, exactly as it already does for the API's appdb.
//
// No DatabaseSchemaName here, unlike the Marten and Polecat hosts: SQLite has no schemas, so the
// separation those two get from a schema comes from this being its own file.
var eventStore = builder.Services.AddFisher(options =>
    {
        options.Connection(builder.Configuration.GetConnectionString("eventstore")!);

        // Async, not Inline: the daemon owns its own checkpoint, so this read model can be rebuilt
        // independently and reports lag. The cost is eventual consistency on GET /widgets/{id} -
        // see WidgetSummaryProjection for the full note and the one-line switch to Inline.
        options.Projections.Add<WidgetSummaryProjection>(ProjectionLifecycle.Async);

        // Schema creation is Development-only, for the same reason template.api gates
        // InitializeDatabaseAsync: a scaffold must not deploy schema changes unreviewed.
        if (!builder.Environment.IsDevelopment())
        {
            options.AutoCreateSchemaObjects = JasperFx.AutoCreate.None;
        }
    })
    // Wires the Fisher/Wolverine transactional outbox: outgoing messages are stored in the same
    // SQLite transaction that appends the events, so the two cannot disagree.
    .IntegrateWithWolverine();

if (builder.Environment.IsDevelopment())
{
    // Creates the schema during startup, and it has to be registered BEFORE AddAsyncDaemon below.
    // Both are hosted services and they start in registration widget; the daemon's high-water
    // detector queries fi_event_progression on its very first tick, before anything has lazily
    // created it, so the reverse widget fails the whole host with "no such table".
    eventStore.ApplyAllDatabaseChangesOnStartup();
}

// Solo rather than HotCold: a SQLite file is single-writer and this host is not something you scale
// out, so there is no election to hold. HotCold would be claiming a lock nobody contests.
eventStore.AddAsyncDaemon(DaemonMode.Solo);

builder.UseWolverine(options =>
{
    options.ServiceName = "template.events";

    // Wraps every handler in a Fisher transaction and commits it, which is why no handler in this
    // project calls SaveChangesAsync itself.
    options.Policies.AutoApplyTransactions();

    // Durable local queues on the SQLite outbox by default, so `aspire run` on this branch still
    // needs no Docker. Azure Service Bus only when Events:Transport says so, which is the deployed
    // default and, locally, an explicit opt-in to the emulator's containers. Shared by all three
    // hosts - see ServiceBusTransport.
    options.UseConfiguredTransport(builder.Configuration);
});

var app = builder.Build();

// Not gated on Development, matching template.api: the Swagger UI page below fetches this document
// from the browser, so gating it would leave the UI rendering an error everywhere else.
app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "template.events v1");
    options.RoutePrefix = "api-docs";
    options.DocumentTitle = "template.events";
});

// No UseHttpsRedirection, matching template.api: the AppHost pins the http launch profile.
app.UseAuthorization();

app.MapControllers();

// /health and /alive, used by the AppHost to gate this resource.
app.MapDefaultEndpoints();

app.Run();
