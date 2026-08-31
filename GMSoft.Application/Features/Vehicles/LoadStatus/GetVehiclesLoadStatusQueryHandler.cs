using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.LoadStatus;

public class GetVehiclesLoadStatusQueryHandler
    : IRequestHandler<GetVehiclesLoadStatusQuery, IReadOnlyList<VehicleLoadStatusDto>>
{
    private readonly IVehicleRepository _vehicles;

    public GetVehiclesLoadStatusQueryHandler(IVehicleRepository vehicles)
    {
        _vehicles = vehicles;
    }

    public async Task<IReadOnlyList<VehicleLoadStatusDto>> Handle(
        GetVehiclesLoadStatusQuery request,
        CancellationToken cancellationToken)
        => await _vehicles.GetLoadStatusAsync(cancellationToken);
}
