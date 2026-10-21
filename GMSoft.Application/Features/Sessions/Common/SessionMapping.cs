using GMSoft.Domain.Entities;
using GMSoft.Application.Common;

namespace GMSoft.Application.Features.Sessions.Common;

public static class SessionMapping
{
    /// <summary>Para mostrar: «San Miguel de Tucumán → Tafí Viejo», o la zona sola.</summary>
    public static string ZoneLabel(DeliverySession session) => string.Join(" → ", Zones(session).Select(z => z.ZoneName));

    /// <summary>Las zonas en orden; si no se cargaron, la principal sola.</summary>
    public static IReadOnlyList<SessionZoneDto> Zones(DeliverySession session) => session.Zones.Count > 0
        ? session.Zones.OrderBy(z => z.Position).Select(z => new SessionZoneDto(z.ZoneId, z.Zone?.Name ?? string.Empty)).ToList()
        : [new SessionZoneDto(session.ZoneId, session.Zone?.Name ?? string.Empty)];

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
        Stock:               stock)
        {
            Zones = Zones(session),
            OpenedOutsideWorkingHours = session.OpenedOutsideWorkingHours,
            ClosedOutsideWorkingHours = session.ClosedOutsideWorkingHours,
            SettlementOutsideWorkingHours = session.CashSettlement?.IsOutsideWorkingHours ?? false,
            IsSettled = session.CashSettlement is not null,
            OpenedByUserId = session.OpenedByUserId,
            ClosedByUserId = session.ClosedByUserId,
            OutsideWorkingHoursEvents = GMSoft.Application.Features.WorkingHours.OutsideWorkingHoursEventDto.From(session),
            RouteDays = session.RouteDays ?? [BusinessTime.IsoDayOfWeek(session.OpenedAt)],
            Restocks = session.Restocks.OrderBy(r => r.OccurredAt).ThenBy(r => r.Id)
                .Select(SessionRestockDto.From).ToList()
        };
}
