using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using template.api.tests.Infrastructure;
using template.data;

namespace template.api.tests.Harness;

/// <summary>
/// Covers <see cref="WeatherSeeder"/> directly, because its interesting branch is the one the
/// application's own startup almost never takes.
/// </summary>
/// <remarks>
/// Both containers use WithDataVolume, so rows outlive a restart and the seeder has to decide
/// whether what it finds is still usable. A fresh database exercises only the "empty" path; the
/// stale path is what runs on a machine that was last used a week ago, and it is the one that
/// deletes. Left untested it would be the branch nobody discovers is broken until a Monday.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WeatherSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private WeatherDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new WeatherDbContext(
            new DbContextOptionsBuilder<WeatherDbContext>().UseSqlite(_connection).Options);

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SeedAsync_OnAnEmptyDatabase_WritesFiveFutureDays()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        await WeatherSeeder.SeedAsync(_context);

        var forecasts = await _context.Forecasts.OrderBy(forecast => forecast.Date).ToListAsync();

        forecasts.ShouldSatisfyAllConditions(
            () => forecasts.Count.ShouldBe(5),
            () => forecasts.Select(forecast => forecast.Date)
                .ShouldBe(Enumerable.Range(1, 5).Select(today.AddDays)),
            () => forecasts.ShouldAllBe(forecast => forecast.Summary != null));
    }

    [Fact]
    public async Task SeedAsync_WhenTheSeedIsCurrent_LeavesItAlone()
    {
        await WeatherSeeder.SeedAsync(_context);
        var original = await _context.Forecasts.OrderBy(forecast => forecast.Date).ToListAsync();

        await WeatherSeeder.SeedAsync(_context);

        var after = await _context.Forecasts.OrderBy(forecast => forecast.Date).ToListAsync();

        after.Select(forecast => (forecast.Id, forecast.Date, forecast.TemperatureC, forecast.Summary))
            .ShouldBe(original.Select(forecast => (forecast.Id, forecast.Date, forecast.TemperatureC, forecast.Summary)));
    }

    [Fact]
    public async Task SeedAsync_WhenTheSeedIsStale_ReplacesEveryRow()
    {
        // What a machine that was last run a fortnight ago looks like. This is the branch that
        // deletes, and the only test that exercises it.
        var today = DateOnly.FromDateTime(DateTime.Now);

        _context.Forecasts.AddRange(Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = today.AddDays(-14 + index),
            TemperatureC = 0,
            Summary = "Stale"
        }));

        await _context.SaveChangesAsync();

        await WeatherSeeder.SeedAsync(_context);

        var forecasts = await _context.Forecasts.OrderBy(forecast => forecast.Date).ToListAsync();

        forecasts.ShouldSatisfyAllConditions(
            () => forecasts.Count.ShouldBe(5),
            () => forecasts.ShouldNotContain(forecast => forecast.Summary == "Stale"),
            () => forecasts.Select(forecast => forecast.Date)
                .ShouldBe(Enumerable.Range(1, 5).Select(today.AddDays)));
    }
}
