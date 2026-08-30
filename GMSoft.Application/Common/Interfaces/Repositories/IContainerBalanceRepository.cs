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
    /// Mueve el saldo de un cliente, creando la fila si es la primera vez. Un solo
    /// lugar hace este upsert: cuando estaba repetido, dos llamados en la misma
    /// transaccion no veian la fila del otro e intentaban crear dos saldos para el
    /// mismo par cliente-producto.
    /// </summary>
    Task AdjustAsync(
        Guid customerId,
        Guid productId,
        int delta,
        CancellationToken cancellationToken = default);

    /// <summary>Todos los envases que tiene un cliente en su poder.</summary>
    Task<IReadOnlyList<CustomerContainerBalance>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
