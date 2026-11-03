namespace GMSoft.Application.Features.Reports.VehicleKilometers;

public record VehicleKilometersDto(DateOnly From, DateOnly To, IReadOnlyList<VehicleKilometersLineDto> Vehicles);

/// <summary>
/// Km recorridos por un vehiculo en sus salidas cerradas. La mediana es la referencia:
/// un kilometraje inventado mueve el promedio pero no la mediana.
/// </summary>
/// <param name="KilometersBetweenSessions">
/// Km entre la vuelta de una salida y la salida siguiente: hechos fuera de reparto o mal cargados.
/// </param>
public record VehicleKilometersLineDto(
    Guid VehicleId, string VehicleName, string LicensePlate,
    int Sessions, long TotalKilometers,
    decimal? AveragePerSession, decimal? MedianPerSession, int? MinPerSession, int? MaxPerSession,
    int DaysWorked, decimal? AveragePerDay, int ZeroKilometerSessions, long KilometersBetweenSessions,
    IReadOnlyList<RouteKilometersDto> Routes);

/// <summary>Un recorrido es el conjunto de zonas de la salida, en el orden en que las recorre.</summary>
public record RouteKilometersDto(
    string Route, int Sessions,
    decimal? AveragePerSession, decimal? MedianPerSession, int? MinPerSession, int? MaxPerSession);

/// <summary>Una salida cerrada tal como sale de la base, sin calcular nada.</summary>
public record SessionKilometersRow(
    Guid VehicleId, string VehicleName, string LicensePlate, DateTime OpenedAt,
    int KilometersAtOpen, int KilometersAtClose, IReadOnlyList<string> Zones);
