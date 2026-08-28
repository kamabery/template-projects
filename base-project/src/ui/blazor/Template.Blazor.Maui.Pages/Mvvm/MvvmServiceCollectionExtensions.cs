using Microsoft.Extensions.DependencyInjection;
using Refit;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.ViewModels;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// Registration for everything this component library needs. Both hosts call these, which is what
/// stops them drifting apart - in the sibling Spruce.Next repo the web host registers two
/// ViewModels and the MAUI host registers one, so one page is broken on mobile only.
/// </summary>
public static class MvvmServiceCollectionExtensions
{
    /// <summary>
    /// Registers every ViewModel in this library.
    /// </summary>
    /// <remarks>
    /// Transient, not scoped. In a WebAssembly host there is exactly one DI scope for the whole
    /// application lifetime, and a MAUI BlazorWebView's scope spans the entire WebView - so a
    /// scoped ViewModel is effectively a singleton, shared across every visit to a page and across
    /// two simultaneous instances of the same component. State leaks between navigations and
    /// OnInitializedAsync re-runs against already-populated data. Genuinely shared state belongs in
    /// Stores/ instead.
    /// </remarks>
    public static IServiceCollection AddSharedViewModels(this IServiceCollection services)
    {
        services.AddTransient<CounterViewModel>();
        services.AddTransient<WeatherForecastViewModel>();

        return services;
    }

    /// <summary>
    /// Registers the Refit clients this library's ViewModels depend on, pointed at
    /// <paramref name="apiBaseAddress"/>. Each host supplies its own address: the WASM host reads it
    /// from wwwroot/appsettings.json, the MAUI host from its embedded appsettings.json.
    /// </summary>
    public static IServiceCollection AddSharedProviders(this IServiceCollection services, Uri apiBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);

        services
            .AddRefitClient<IWeatherForecastProvider>()
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddStandardResilienceHandler();

        return services;
    }
}
