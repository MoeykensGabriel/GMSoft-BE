using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Sessions.Common;

public static class SessionMapping
{
    public static SessionDto ToDto(
        DeliverySession session,
        IReadOnlyList<SessionStockLineDto> stock) => new(
        Id:                  session.Id,
        DriverId:            session.DriverId,
        DriverName:          session.Driver is null
                                 ? string.Empty
                                 : $"{session.Driver.FirstName} {session.Driver.LastName}".Trim(),
        VehicleId:           session.VehicleId,
        VehicleName:         session.Vehicle?.Name ?? string.Empty,
        VehicleLicensePlate: session.Vehicle?.LicensePlate ?? string.Empty,
        ZoneId:              session.ZoneId,
        ZoneName:            session.Zone?.Name ?? string.Empty,
        OpenedAt:            session.OpenedAt,
        ClosedAt:            session.ClosedAt,
        KilometersAtOpen:    session.KilometersAtOpen,
        KilometersAtClose:   session.KilometersAtClose,
        Status:              session.Status,
        Stock:               stock);
}
