using Microsoft.EntityFrameworkCore;

namespace template.data;

/// <summary>
/// Puts five days of forecast in the table so both front ends have something to render.
/// </summary>
/// <remarks>
/// The container carries a data volume, so rows outlive a restart and a naive "seed when empty"
/// check would keep serving whatever dates were current the first time the volume was created. A
/// machine left alone over a weekend would then show a forecast for last Tuesday. So the check is
/// staleness, not emptiness: if the newest row is no longer five days out, the table is rewritten.
/// </remarks>
public static class WeatherSeeder
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public static async Task SeedAsync(WeatherDbContext context, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var newest = await context.Forecasts.MaxAsync(f => (DateOnly?)f.Date, cancellationToken);

        if (newest is not null && newest.Value >= today.AddDays(5))
        {
            return;
        }

        // Materialised first: RemoveRange would otherwise iterate a streaming query while
        // removing the entities it is producing.
        context.Forecasts.RemoveRange(await context.Forecasts.ToListAsync(cancellationToken));

        context.Forecasts.AddRange(Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = today.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }));

        await context.SaveChangesAsync(cancellationToken);
    }
}
