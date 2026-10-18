using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Polecat;
using template.events.Abstractions;
using template.events.Messaging;
using template.events.Projections;
using template.events.Storage;
using Wolverine;
using Wolverine.Polecat;

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks and OpenTelemetry - the same stock Aspire file
// template.api uses. It is what supplies /health, which the AppHost gates this resource on, and
// what puts the Polecat and Wolverine spans under the incoming request in the dashboard's traces.
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

// The event store. This host is the SQL Server one: Polecat is Marten's API on SQL Server 2025,
// which it needs specifically for the native json column type, so the AppHost pins
// mssql/server:2025-latest and only declares this host when Database:Provider is sqlserver.
//
// Unlike the Marten host, the store here is a second database on the SAME container the API uses.
// There is no reason to run a second SQL Server engine: the two stores are already isolated by
// database, and a second engine plus the Service Bus emulator's own companion would put three of
// them on a development machine.
builder.Services.AddPolecat(options =>
    {
        options.Connection(builder.Configuration.GetConnectionString("eventstore")!);

        // Keeps the event tables out of dbo, so an event store sharing a server with anything else
        // stays visibly separate.
        options.DatabaseSchemaName = "event_store";

        // Async, not Inline: the daemon owns its own checkpoint, so this read model can be rebuilt
        // independently and reports lag. The cost is eventual consistency on GET /widgets/{id} -
        // see WidgetSummaryProjection for the full note and the one-line switch to Inline.
        options.Projections.Add<WidgetSummaryProjection>(ProjectionLifecycle.Async);

        // Schema creation is Development-only, for the same reason template.api gates
        // InitializeDatabaseAsync: a scaffold must not deploy schema changes unreviewed. A real
        // deployment applies the schema from its pipeline instead.
        if (!builder.Environment.IsDevelopment())
        {
            options.AutoCreateSchemaObjects = JasperFx.AutoCreate.None;
        }
    })
    .UseLightweightSessions()
    // Wires the Polecat/Wolverine transactional outbox: outgoing messages are stored in the same
    // SQL Server transaction that appends the events, so the two cannot disagree.
    .IntegrateWithWolverine()
    // HotCold: exactly one node runs the projection daemon at a time, elected through the database.
    .AddAsyncDaemon(DaemonMode.HotCold);

builder.UseWolverine(options =>
{
    options.ServiceName = "template.events";

    // Wraps every handler in a Polecat transaction and commits it, which is why no handler in this
    // project calls SaveChangesAsync itself.
    options.Policies.AutoApplyTransactions();

    // Durable local queues on the SQL Server outbox by default, Azure Service Bus when
    // Events:Transport says so. Shared by all three hosts - see ServiceBusTransport.
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
