using Mapster;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Vehicles.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.GetById;

public class GetVehicleByIdQueryHandler : IRequestHandler<GetVehicleByIdQuery, VehicleDto>
{
    private readonly IVehicleRepository _vehicles;

    public GetVehicleByIdQueryHandler(IVehicleRepository vehicles)
    {
        _vehicles = vehicles;
    }

    public async Task<VehicleDto> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.Id);

        return vehicle.Adapt<VehicleDto>();
    }
}
