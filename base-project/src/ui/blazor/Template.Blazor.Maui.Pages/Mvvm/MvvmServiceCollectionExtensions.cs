using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refit;
using Template.Blazor.Maui.Pages.Auth;
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
        services.AddTransient<WidgetsViewModel>();

        return services;
    }

    /// <summary>
    /// Registers the generated Refit client this library's ViewModels depend on, pointed at
    /// <paramref name="apiBaseAddress"/>. Each host supplies its own address: the WASM host reads it
    /// from wwwroot/appsettings.json, the MAUI host from its embedded appsettings.json.
    /// </summary>
    /// <param name="configureApi">Lets a host add to the API client's pipeline - the WebAssembly host
    /// puts its bearer-token handler here when sign-in is on.</param>
    /// <remarks>
    /// Also registers <see cref="SignInOff"/> as <see cref="ISignIn"/> unless the host registered
    /// its own first, so the layout always has one to ask.
    /// </remarks>
    public static IServiceCollection AddSharedProviders(
        this IServiceCollection services, Uri apiBaseAddress, Action<IHttpClientBuilder>? configureApi = null)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);

        var api = services
            .AddRefitClient<IApi>()
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress);
        api.AddStandardResilienceHandler();

        // After the resilience handler, so inside it: a retried request asks for its token again.
        configureApi?.Invoke(api);

        services.TryAddScoped<ISignIn, SignInOff>();

        return services;
    }
}
