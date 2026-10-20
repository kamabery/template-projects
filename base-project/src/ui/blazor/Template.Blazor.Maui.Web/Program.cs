using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Auth;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Web;
using Template.Blazor.Maui.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents();

// Both hosts call the same two extension methods from the shared library, which is what keeps
// their DI graphs from drifting apart.
builder.Services.AddSharedViewModels();

var apiBaseAddress = builder.Configuration["ApiBaseAddress"]
    ?? throw new InvalidOperationException("ApiBaseAddress is missing from wwwroot/appsettings.json.");

// Sign-in only when the API asks for it (GET /auth/config). Registered before AddSharedProviders,
// which otherwise registers SignInOff for the layout. Scopes come from the API too, so the access
// token's audience is whatever that environment's API expects.
var auth = await AuthSettings.FetchAsync(new Uri(apiBaseAddress));
if (auth.Enabled)
{
    builder.Services.AddOidcAuthentication(options =>
    {
        options.ProviderOptions.Authority = auth.Authority;
        options.ProviderOptions.ClientId = auth.ClientId;
        options.ProviderOptions.ResponseType = "code";
        options.ProviderOptions.DefaultScopes.Clear();
        foreach (var scope in (auth.Scope ?? "openid profile").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.ProviderOptions.DefaultScopes.Add(scope);
        }
    });
    builder.Services.AddScoped<ISignIn, WebSignIn>();
    builder.Services.AddTransient<OptionalBearerHandler>();
}

builder.Services.AddSharedProviders(
    new Uri(apiBaseAddress),
    api => { if (auth.Enabled) api.AddHttpMessageHandler<OptionalBearerHandler>(); });

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
