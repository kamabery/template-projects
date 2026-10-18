using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using template.events.Handlers;

var builder = FunctionsApplication.CreateBuilder(args);

// The same service defaults as every other service: OpenTelemetry and Serilog, with the OTLP sink
// pointed at the dashboard whenever the AppHost supplies an endpoint. Once deployed there is no such
// endpoint, so worker logs go to the console sink, and the Functions host records each invocation -
// success, failure, duration - in Application Insights on its own.
builder.AddServiceDefaults();

// The handler is shared with the events service, where Wolverine resolves it under the local
// transport. Here it is an ordinary transient the Function takes by constructor injection.
builder.Services.AddTransient<WidgetCompletedV1Handler>();

builder.Build().Run();
