using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Mvvm;
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

await builder.Build().RunAsync();
