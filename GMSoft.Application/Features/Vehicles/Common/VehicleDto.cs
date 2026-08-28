using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Vehicles.Common;

public record VehicleDto(
    Guid        Id,
    string      Name,
    string      LicensePlate,
    VehicleType Type,
    int         CurrentKilometers);
