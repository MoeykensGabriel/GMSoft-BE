using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetPendingSummary;

public class GetPendingVehicleLoadSummaryQueryHandler
    : IRequestHandler<GetPendingVehicleLoadSummaryQuery, VehicleLoadSummaryDto>
{
    private readonly IVehicleLoadRepository _loads;

    public GetPendingVehicleLoadSummaryQueryHandler(IVehicleLoadRepository loads)
    {
        _loads = loads;
    }

    public async Task<VehicleLoadSummaryDto> Handle(
        GetPendingVehicleLoadSummaryQuery request,
        CancellationToken cancellationToken)
    {
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
