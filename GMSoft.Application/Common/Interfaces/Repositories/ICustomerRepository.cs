using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    /// <summary>
    /// Listado del reparto. Filtrado por zona y ordenado por RouteOrder es la hoja
    /// de ruta que ve el chofer; sin zona es la vista de la oficina.
    /// </summary>
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        Guid? zoneId,
        bool? onlyActive,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetWithZoneAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Siguiente lugar libre del recorrido de una zona. El cliente nuevo va al final,
    /// que es lo que significa que el orden sea el orden de carga.
    /// </summary>
    Task<int> GetNextRouteOrderAsync(Guid zoneId, CancellationToken cancellationToken = default);

    /// <summary>Si ya tiene visitas, movimientos de envase o pagos.</summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
