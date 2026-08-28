namespace GMSoft.Application.Common.Interfaces;

/// <summary>
/// Abstracción del Unit of Work. Permite guardar cambios sin depender de EF Core en Application.
/// Implementado por AppDbContext en la capa Data.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Corre varias escrituras como una sola operación: o quedan todas o no queda
    /// ninguna. Hace falta cuando un caso de uso guarda más de una vez, por ejemplo
    /// crear la cuenta y el chofer, o registrar una visita con su venta, sus envases
    /// y su deuda.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken cancellationToken = default);
}
