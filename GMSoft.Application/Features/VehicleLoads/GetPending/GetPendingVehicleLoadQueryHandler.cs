using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.VehicleLoads.Common;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetPending;

public class GetPendingVehicleLoadQueryHandler
    : IRequestHandler<GetPendingVehicleLoadQuery, IReadOnlyList<VehicleLoadLineDto>>
{
    private readonly IVehicleLoadRepository _loads;

    public GetPendingVehicleLoadQueryHandler(IVehicleLoadRepository loads)
    {
        _loads = loads;
    }

    public async Task<IReadOnlyList<VehicleLoadLineDto>> Handle(
        GetPendingVehicleLoadQuery request,
        CancellationToken cancellationToken)
    {
        var pendientes = await _loads.GetPendingAsync(request.VehicleId, cancellationToken);

        return pendientes
            .Select(l => new VehicleLoadLineDto(
                l.Id, l.ProductId, l.Product.Detail, l.Quantity, l.LoadedAt))
            .ToList();
    }
}
