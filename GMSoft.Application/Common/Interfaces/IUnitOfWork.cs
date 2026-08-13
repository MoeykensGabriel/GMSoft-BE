namespace GMSoft.Application.Common.Interfaces;

/// <summary>
/// Abstracción del Unit of Work. Permite guardar cambios sin depender de EF Core en Application.
/// Implementado por AppDbContext en la capa Data.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
