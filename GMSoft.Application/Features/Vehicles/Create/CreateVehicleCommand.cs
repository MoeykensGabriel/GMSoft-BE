using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Create;

public record CreateVehicleCommand(
    string      Name,
    string      LicensePlate,
    VehicleType Type,
    int         CurrentKilometers) : IRequest<Guid>;
