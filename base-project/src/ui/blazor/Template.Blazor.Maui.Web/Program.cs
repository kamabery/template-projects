using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents();

// Both hosts call the same two extension methods from the shared library, which is what keeps
// their DI graphs from drifting apart.
builder.Services.AddSharedViewModels();

var apiBaseAddress = builder.Configuration["ApiBaseAddress"]
    ?? throw new InvalidOperationException("ApiBaseAddress is missing from wwwroot/appsettings.json.");

builder.Services.AddSharedProviders(new Uri(apiBaseAddress));

// Client-side RUM. A browser has no environment for the AppHost to inject OTEL_* into and this
// host's wwwroot/appsettings.json is a static file that cannot carry the dashboard's per-run API
// key, so the settings come from the API instead - see ClientTelemetryOptions.FetchAsync. Any
// failure there leaves telemetry off rather than blocking startup.
//
// No runtime metrics: the browser runtime is single-threaded, so its GC and thread-pool counters
// describe nothing a developer can act on. The MAUI host does register them.
builder.Services.AddSharedTelemetry(
    (await ClientTelemetryOptions.FetchAsync(new Uri(apiBaseAddress), "blazor-web"))
    with { IsSingleThreadedRuntime = true });

var host = builder.Build();

// The telemetry providers are registered as factories, so something has to ask for them before they
// start. See ActivateSharedTelemetry.
host.Services.ActivateSharedTelemetry();

await host.RunAsync();
