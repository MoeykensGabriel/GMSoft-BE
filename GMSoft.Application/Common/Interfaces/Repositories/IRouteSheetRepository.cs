using GMSoft.Application.Features.Reports.RouteSheet;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IRouteSheetRepository
{
    /// <summary>
    /// Los clientes activos del camión que se visitan el día <paramref name="isoDay"/>
    /// (1 lunes … 7 domingo), sin los que tienen una baja pedida, igual que la hoja del
    /// chofer. Nulo si el camión no existe.
    /// </summary>
    Task<RouteSheetDto?> GetAsync(Guid vehicleId, DateOnly date, int isoDay, CancellationToken cancellationToken = default);
}
