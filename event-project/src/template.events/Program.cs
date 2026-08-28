using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using template.events.Projections;
using Wolverine;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks and OpenTelemetry - the same stock Aspire file
// template.api uses. It is what supplies /health, which the AppHost gates this resource on, and
// what puts the Marten and Wolverine spans under the incoming request in the dashboard's traces.
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Injected rather than called statically so the handlers stay pure functions the unit tests can
// call with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// The event store. Always Postgres - Marten has no other backend - and deliberately a different
// database from template.api's weatherdb, which may be SQL Server. The AppHost starts a dedicated
// events-pg container and injects ConnectionStrings__eventstore; the fallback in
// appsettings.Development.json is what lets `dotnet run` work standalone against a local Postgres.
builder.Services.AddMarten(options =>
    {
        options.Connection(builder.Configuration.GetConnectionString("eventstore")!);

        // Keeps the event tables out of the public schema, so an event store sharing a database
        // with anything else stays visibly separate.
        options.DatabaseSchemaName = "event_store";

        // Async, not Inline: the daemon owns its own checkpoint, so this read model can be rebuilt
        // independently and reports lag. The cost is eventual consistency on GET /orders/{id} -
        // see OrderSummaryProjection for the full note and the one-line switch to Inline.
        options.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Async);

        // Schema creation is Development-only, for the same reason template.api gates
        // InitializeDatabaseAsync: a scaffold must not deploy schema changes unreviewed. A real
        // deployment applies Marten's schema from its pipeline instead.
        if (!builder.Environment.IsDevelopment())
        {
            options.AutoCreateSchemaObjects = JasperFx.AutoCreate.None;
        }
    })
    .UseLightweightSessions()
    // Wires the Marten/Wolverine transactional outbox: outgoing messages are stored in the same
    // Postgres transaction that appends the events, so the two cannot disagree.
    .IntegrateWithWolverine()
    // HotCold: exactly one node runs the projection daemon at a time, elected through the database.
    // Correct whether the scaffold runs one instance or ten.
    .AddAsyncDaemon(DaemonMode.HotCold);

builder.UseWolverine(options =>
{
    options.ServiceName = "template.events";

    // Wraps every handler in a Marten transaction and commits it, which is why no handler in this
    // project calls SaveChangesAsync itself.
    options.Policies.AutoApplyTransactions();

    // No broker is configured, on purpose - see the README. Messages route to local in-process
    // queues, and this makes those queues durable: they are backed by the same Postgres outbox
    // tables, so a message survives a restart between commit and dispatch. Adding a real transport
    // later is a registration here and nothing else.
    options.Policies.UseDurableLocalQueues();
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
