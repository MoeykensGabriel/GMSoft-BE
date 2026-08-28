using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IZoneRepository : IRepository<Zone>
{
    Task<(IReadOnlyList<Zone> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyActive,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Si ya tiene clientes o sesiones. Con historia se desactiva, no se elimina.</summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
