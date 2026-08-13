using GMSoft.Domain.Common;

namespace GMSoft.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repositorio genérico con operaciones básicas para cualquier entidad BaseEntity.
/// Los repositorios específicos extienden este contrato con queries propias.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity); // Soft delete — interceptado por SaveChangesAsync
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
