namespace GMSoft.Application.Features.Drivers.Common;

public record DriverDto(
    Guid    Id,
    string  FirstName,
    string  LastName,
    string  DocumentNumber,
    string  Phone,
    Guid?   VehicleId,
    string? VehicleName,
    string? VehicleLicensePlate,
    string? UserName,
    string? Email,
    bool    IsActive);
