namespace template.data.Repositories;

/// <summary>
/// The generic data-access contract every entity is reached through.
/// </summary>
/// <remarks>
/// Only <see cref="GetAllAsync"/> has a caller today - WidgetsController reads and nothing
/// writes. The rest is here because a scaffold's job is to show the shape a generated project
/// grows into, not because a use case is pending. Delete the unused half if your project never
/// needs it.
/// </remarks>
public interface IRepository<TEntity> where TEntity : class
{
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
