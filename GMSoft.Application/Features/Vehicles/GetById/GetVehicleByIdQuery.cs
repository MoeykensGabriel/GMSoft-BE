using GMSoft.Application.Features.Vehicles.Common;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.GetById;

public record GetVehicleByIdQuery(Guid Id) : IRequest<VehicleDto>;
