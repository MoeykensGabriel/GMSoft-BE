using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IDriverRepository : IRepository<Driver>
{
    Task<(IReadOnlyList<Driver> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyActive,
        CancellationToken cancellationToken = default);

    /// <summary>Trae el chofer con su vehiculo, para poder mostrar patente sin otra consulta.</summary>
    Task<Driver?> GetWithVehicleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByDocumentAsync(
        string documentNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
