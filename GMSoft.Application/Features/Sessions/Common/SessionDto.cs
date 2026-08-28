using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.Common;

public record SessionDto(
    Guid         Id,
    Guid         DriverId,
    string       DriverName,
    Guid         VehicleId,
    string       VehicleName,
    string       VehicleLicensePlate,
    Guid         ZoneId,
    string       ZoneName,
    DateTime     OpenedAt,
    DateTime?    ClosedAt,
    int          KilometersAtOpen,
    int?         KilometersAtClose,
    SessionStatus Status,
    IReadOnlyList<SessionStockLineDto> Stock);
