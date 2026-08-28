using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IContainerUnitRepository : IRepository<ContainerUnit>
{
    Task<(IReadOnlyList<ContainerUnit> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        Guid? productId,
        ContainerUnitStatus? status,
        Guid? customerId,
        CancellationToken cancellationToken = default);

    Task<ContainerUnit?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySerialNumberAsync(
        string serialNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Las unidades que tiene un cliente. Para los productos seguidos por numero no
    /// se lleva saldo por cantidad: cuantas tiene se cuenta desde aca, y asi no hay
    /// dos numeros para lo mismo.
    /// </summary>
    Task<IReadOnlyList<ContainerUnit>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
