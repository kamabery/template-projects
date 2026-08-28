using Refit;
using Template.Blazor.Maui.Pages.Models;

namespace Template.Blazor.Maui.Pages.Providers;

/// <summary>
/// Typed HTTP client for the weather endpoint. Refit interfaces live in this library (rather than in
/// either host) so both hosts talk to the API through exactly the same contract.
/// </summary>
public interface IWeatherForecastProvider
{
    /// <summary>
    /// Mirrors template.api's <c>WeatherForecastController</c>, which is attribute-routed at
    /// <c>[Route("[controller]")]</c>. Spelled to match the controller name even though ASP.NET
    /// Core's routing is case-insensitive.
    /// </summary>
    [Get("/WeatherForecast")]
    Task<IReadOnlyList<WeatherForecast>> GetForecastAsync(CancellationToken cancellationToken = default);
}
