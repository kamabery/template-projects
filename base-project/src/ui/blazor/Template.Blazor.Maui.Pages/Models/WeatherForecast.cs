namespace Template.Blazor.Maui.Pages.Models;

/// <summary>One day's forecast, as returned by template.api.</summary>
/// <remarks>
/// A record with its own computed <see cref="TemperatureF"/> rather than a copy of the API's
/// class. The API serializes a <c>temperatureF</c> it computed itself; System.Text.Json ignores
/// a JSON property with no settable member, so it lands here harmlessly and the client does the
/// conversion locally.
/// </remarks>
public sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
