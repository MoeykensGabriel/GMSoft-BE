using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetPendingSummary;

public class GetPendingVehicleLoadSummaryQueryHandler
    : IRequestHandler<GetPendingVehicleLoadSummaryQuery, VehicleLoadSummaryDto>
{
    private readonly IVehicleLoadRepository _loads;
    private readonly IDriverRepository _drivers;
    private readonly ICurrentUserService _currentUser;

    public GetPendingVehicleLoadSummaryQueryHandler(IVehicleLoadRepository loads, IDriverRepository drivers,
        ICurrentUserService currentUser)
    {
        _loads       = loads;
        _drivers     = drivers;
        _currentUser = currentUser;
    }

    public async Task<VehicleLoadSummaryDto> Handle(
        GetPendingVehicleLoadSummaryQuery request,
        CancellationToken cancellationToken)
    {
        // El chofer ve solo la carga de su camión: la de otro no le corresponde.
        if (!_currentUser.IsInRole(AppRoles.Admin))
        {
            var chofer = _currentUser.DriverId is Guid driverId
                ? await _drivers.GetByIdAsync(driverId, cancellationToken)
                : null;
            if (chofer?.VehicleId != request.VehicleId)
                throw new ForbiddenException("Ese camión no está asignado a tu usuario.");
        }

        var pendientes = await _loads.GetPendingAsync(request.VehicleId, cancellationToken);

        // Por id y no por nombre: dos productos distintos pueden llamarse igual, y
        // el mismo producto cargado en dos tandas es una sola linea.
        var lineas = pendientes
            .GroupBy(l => l.ProductId)
            .Select(g => new VehicleLoadSummaryLineDto(g.Key, g.First().Product.Detail, g.Sum(l => l.Quantity)))
            .OrderBy(l => l.ProductDetail)
            .ToList();

        var dias = pendientes
            .SelectMany(l => l.RouteDays ?? [])
            .Distinct()
            .Order()
            .ToArray();

        return new VehicleLoadSummaryDto(dias, lineas);
    }
}
