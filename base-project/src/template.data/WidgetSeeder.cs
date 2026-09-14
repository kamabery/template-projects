using Microsoft.EntityFrameworkCore;

namespace template.data;

/// <summary>
/// Puts five rows in the table so both front ends have something to render.
/// </summary>
/// <remarks>
/// The container carries a data volume, so rows outlive a restart and a naive "seed when empty"
/// check would keep serving whatever <see cref="Widget.UpdatedOn"/> dates were current the first
/// time the volume was created. A machine left alone over a weekend would then show a sample that
/// claims it was last touched last Tuesday. So the check is staleness, not emptiness: if the newest
/// row predates today, the table is rewritten.
/// </remarks>
public static class WidgetSeeder
{
    private static readonly string[] Names =
    [
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett"
    ];

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var newest = await context.Widgets.MaxAsync(w => (DateOnly?)w.UpdatedOn, cancellationToken);

        if (newest is not null && newest.Value >= today)
        {
            return;
        }

        // Materialised first: RemoveRange would otherwise iterate a streaming query while
        // removing the entities it is producing.
        context.Widgets.RemoveRange(await context.Widgets.ToListAsync(cancellationToken));

        // Names are taken in order rather than at random because the column is uniquely indexed,
        // and five random draws from ten would collide often enough to matter.
        context.Widgets.AddRange(Enumerable.Range(0, 5).Select(index => new Widget
        {
            Name = Names[index],
            Quantity = Random.Shared.Next(0, 25),
            UpdatedOn = today.AddDays(-index)
        }));

        await context.SaveChangesAsync(cancellationToken);
    }
}
