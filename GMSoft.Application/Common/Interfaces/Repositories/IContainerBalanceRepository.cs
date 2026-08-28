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

    /// <summary>Todos los envases que tiene un cliente en su poder.</summary>
    Task<IReadOnlyList<CustomerContainerBalance>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
