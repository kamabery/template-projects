using Microsoft.EntityFrameworkCore;

namespace template.data.Repositories;

/// <summary>
/// The one <see cref="IRepository{TEntity}"/> implementation, over <see cref="WeatherDbContext"/>.
/// </summary>
/// <remarks>
/// Registered open-generic (see PersistenceConfigurator), so asking for IRepository&lt;Thing&gt;
/// resolves without a registration per entity. Each mutating method saves rather than deferring to
/// a unit of work: there is no operation here that spans two entities, and a UoW that only ever
/// wraps one SaveChanges is ceremony.
/// </remarks>
public class EfRepository<TEntity>(WeatherDbContext context) : IRepository<TEntity>
    where TEntity : class
{
    private DbSet<TEntity> Set => context.Set<TEntity>();

    // AsNoTracking: every caller of this method reads and discards. Tracking would build change-
    // tracking entries for rows nothing is going to modify.
    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Set.FindAsync([id], cancellationToken);

        if (entity is null)
        {
            return;
        }

        Set.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
