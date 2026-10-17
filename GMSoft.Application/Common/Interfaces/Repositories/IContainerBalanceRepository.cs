using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IContainerBalanceRepository : IRepository<CustomerContainerBalance>
{
    /// <summary>
    /// El saldo de un cliente para un producto, o nulo si nunca tuvo envases de ese
    /// producto. Se trae con tracking porque se actualiza en la misma transaccion.
    /// </summary>
    Task<CustomerContainerBalance?> GetAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mueve el saldo de un cliente, creando la fila si es la primera vez. Es una sola
    /// sentencia atómica en la base: dos operaciones simultáneas sobre el mismo saldo
    /// suman las dos. Va dentro de la transacción del llamador.
    /// </summary>
    Task AdjustAsync(
        Guid customerId,
        Guid productId,
        int delta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// La cantidad actual, con la fila bloqueada hasta que termine la transacción. Para
    /// decisiones que dependen del saldo (un ajuste a la cantidad real, una pérdida): sin el
    /// bloqueo, una venta simultánea cambia el saldo entre la lectura y la escritura.
    /// </summary>
    Task<int> LockQuantityAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Todos los envases que tiene un cliente en su poder.</summary>
    Task<IReadOnlyList<CustomerContainerBalance>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
