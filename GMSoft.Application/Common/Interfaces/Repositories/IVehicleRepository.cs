using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByLicensePlateAsync(
        string licensePlate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Si ya salio a repartir. Con historia no se elimina.</summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
