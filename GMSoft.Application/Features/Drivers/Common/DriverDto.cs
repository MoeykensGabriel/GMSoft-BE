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
    bool    IsActive)
{
    /// <summary>Solo en la lista de choferes de la oficina; nula si no se puede mostrar.</summary>
    public string? Password { get; init; }
}
