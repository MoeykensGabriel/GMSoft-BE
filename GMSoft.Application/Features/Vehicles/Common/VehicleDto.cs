using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Vehicles.Common;

public record VehicleDto(
    Guid        Id,
    string      Name,
    string      LicensePlate,
    VehicleType Type,
    int         CurrentKilometers,
    // La próxima salida lee el km del tablero (ver Vehicle.KilometersCheckPending).
    bool        KilometersCheckPending = false,
    // Desde cuándo el km es confiable; nulo si nunca se aceptó una lectura.
    DateTime?   KilometersCheckedAt = null);
