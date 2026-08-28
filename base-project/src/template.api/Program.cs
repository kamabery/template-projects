using template.api.Services;
using template.api.Startup;

var builder = WebApplication.CreateBuilder(args);

// Service discovery, resilience, health checks and OpenTelemetry.
// Telemetry is exported over OTLP whenever the Aspire AppHost supplies
// OTEL_EXPORTER_OTLP_ENDPOINT; running the API standalone is unaffected.
builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// For the Blazor WebAssembly host only - see CorsConfigurator for why the other two clients
// need nothing.
builder.ConfigureCorsBuilder();

// EF Core over whichever container the AppHost started - see PersistenceConfigurator for why both
// providers are wired and why the connection string arrives rather than being configured here.
builder.ConfigurePersistenceBuilder();

// Singleton on purpose: the point of an injected fault is that it persists across requests, so a
// developer can arm one and then trigger it from the UI.
builder.Services.AddSingleton<HarnessFaultInjector>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Not gated on Development, unlike /health and /harness/*: the Swagger UI page below fetches this
// document from the browser, so gating it would leave the UI rendering an error everywhere else.
app.MapOpenApi();

// Swagger UI renders that document at /api-docs, with a working "Try it out" button. This package
// is UI only: it carries the swagger-ui bundle and no document generator, and has no dependencies
// of its own, so AddOpenApi() above remains the only thing producing the spec - SwaggerEndpoint
// just points it at that document. RoutePrefix has to be set: it defaults to "swagger", and
// /api-docs is the route the Aspire dashboard link, the README and the tests all expect. Every
// asset is an embedded resource on a relative path - no CDN, no webfonts - so the page is fully
// self-contained and works offline. Try it out issues its requests from this same origin, which
// is why CorsConfigurator needs no entry for it.
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "template.api v1");
    options.RoutePrefix = "api-docs";
    options.DocumentTitle = "template.api";
});

// No UseHttpsRedirection: redirecting would break the WebAssembly host's CORS preflight and the
// Android emulator's 10.0.2.2 loopback, both of which talk to this over plain HTTP in development.
app.WarnOnMissingCorsOrigins();
app.UseCors(CorsConfigurator.WasmClientPolicy);

app.UseAuthorization();

app.MapControllers();

// /health and /alive, used by the AppHost to gate dependent resources.
app.MapDefaultEndpoints();

// Applies migrations and seeds the forecast, in Development only. Awaited before Run so the API
// never serves a request against a schema that is not there yet.
await app.InitializeDatabaseAsync();

app.Run();
