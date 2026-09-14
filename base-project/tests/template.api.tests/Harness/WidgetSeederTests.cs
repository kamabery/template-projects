using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using template.api.tests.Infrastructure;
using template.data;

namespace template.api.tests.Harness;

/// <summary>
/// Covers <see cref="WidgetSeeder"/> directly, because its interesting branch is the one the
/// application's own startup almost never takes.
/// </summary>
/// <remarks>
/// Both containers use WithDataVolume, so rows outlive a restart and the seeder has to decide
/// whether what it finds is still usable. A fresh database exercises only the "empty" path; the
/// stale path is what runs on a machine that was last used a week ago, and it is the one that
/// deletes. Left untested it would be the branch nobody discovers is broken until a Monday.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WidgetSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SeedAsync_OnAnEmptyDatabase_WritesFiveRows()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        await WidgetSeeder.SeedAsync(_context);

        var widgets = await _context.Widgets.OrderBy(widget => widget.Name).ToListAsync();

        widgets.ShouldSatisfyAllConditions(
            () => widgets.Count.ShouldBe(5),
            () => widgets.ShouldAllBe(widget => widget.Name.Length > 0),
            // The newest row is today's, which is exactly what the staleness check reads back.
            () => widgets.Max(widget => widget.UpdatedOn).ShouldBe(today));
    }

    [Fact]
    public async Task SeedAsync_WhenTheSeedIsCurrent_LeavesItAlone()
    {
        await WidgetSeeder.SeedAsync(_context);
        var original = await _context.Widgets.OrderBy(widget => widget.Name).ToListAsync();

        await WidgetSeeder.SeedAsync(_context);

        var after = await _context.Widgets.OrderBy(widget => widget.Name).ToListAsync();

        after.Select(widget => (widget.Id, widget.Name, widget.Quantity, widget.UpdatedOn))
            .ShouldBe(original.Select(widget => (widget.Id, widget.Name, widget.Quantity, widget.UpdatedOn)));
    }

    [Fact]
    public async Task SeedAsync_WhenTheSeedIsStale_ReplacesEveryRow()
    {
        // What a machine that was last run a fortnight ago looks like. This is the branch that
        // deletes, and the only test that exercises it.
        var today = DateOnly.FromDateTime(DateTime.Now);

        _context.Widgets.AddRange(Enumerable.Range(1, 5).Select(index => new Widget
        {
            Name = $"Stale {index}",
            Quantity = 0,
            UpdatedOn = today.AddDays(-14)
        }));

        await _context.SaveChangesAsync();

        await WidgetSeeder.SeedAsync(_context);

        var widgets = await _context.Widgets.OrderBy(widget => widget.Name).ToListAsync();

        widgets.ShouldSatisfyAllConditions(
            () => widgets.Count.ShouldBe(5),
            () => widgets.ShouldNotContain(widget => widget.Name.StartsWith("Stale")),
            () => widgets.Max(widget => widget.UpdatedOn).ShouldBe(today));
    }
}
