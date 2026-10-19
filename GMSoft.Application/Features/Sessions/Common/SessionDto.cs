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
    IReadOnlyList<SessionStockLineDto> Stock)
{

    public bool OpenedOutsideWorkingHours { get; init; }
    public bool ClosedOutsideWorkingHours { get; init; }
    public bool SettlementOutsideWorkingHours { get; init; }
    /// <summary>Si ya se cargo la plata que entrego el chofer.</summary>
    public bool IsSettled { get; init; }
    public GMSoft.Application.Features.Customers.RoutePlanning.RouteOrderChangeDto? DriverRouteOrderChange { get; init; }
    public Guid? OpenedByUserId { get; init; }
    public Guid? ClosedByUserId { get; init; }
    public IReadOnlyList<GMSoft.Application.Features.WorkingHours.OutsideWorkingHoursEventDto> OutsideWorkingHoursEvents { get; init; } = [];
    public int[] RouteDays { get; init; } = [];
    public IReadOnlyList<SessionRestockDto> Restocks { get; init; } = [];
}
