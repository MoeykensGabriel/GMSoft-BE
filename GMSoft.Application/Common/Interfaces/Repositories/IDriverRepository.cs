using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IDriverRepository : IRepository<Driver>
{
    /// <summary>Bloquea la fila dentro de una transaccion antes de leer su estado operativo.</summary>
    Task<Driver?> LockAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Driver> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyActive,
        CancellationToken cancellationToken = default);

    /// <summary>Trae el chofer con su vehiculo, para poder mostrar patente sin otra consulta.</summary>
    Task<Driver?> GetWithVehicleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si ya salio a repartir alguna vez. Un chofer con historia no se puede
    /// eliminar: el filtro de soft delete lo sacaria tambien de sus propias
    /// sesiones y esas salidas quedarian sin dueño.
    /// </summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByDocumentAsync(
        string documentNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
