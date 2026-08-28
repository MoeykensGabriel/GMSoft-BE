using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ICustomerPriceRepository : IRepository<CustomerProductPrice>
{
    /// <summary>
    /// Precio particular del cliente para ese producto, o nulo si no tiene. Quien
    /// llama cae al precio del catalogo cuando no hay fila.
    /// </summary>
    Task<decimal?> GetPriceAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerProductPrice>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
