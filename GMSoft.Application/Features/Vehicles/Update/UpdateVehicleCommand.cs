using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Update;

public record UpdateVehicleCommand(
    Guid        Id,
    string      Name,
    string      LicensePlate,
    VehicleType Type,
    int         CurrentKilometers) : IRequest;
