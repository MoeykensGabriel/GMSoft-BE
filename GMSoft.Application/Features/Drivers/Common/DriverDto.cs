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

    /// <summary>
    /// El último kilometraje registrado del camión asignado: al abrir la salida el chofer
    /// no puede poner menos, y sirve para avisarle si tipeó de más.
    /// </summary>
    public int? VehicleKilometers { get; init; }
}
