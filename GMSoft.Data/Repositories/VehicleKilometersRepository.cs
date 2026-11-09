using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.VehicleKilometers;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleKilometersRepository(AppDbContext context) : IVehicleKilometersRepository
{
    public async Task<IReadOnlyList<SessionKilometersRow>> GetClosedSessionsAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        // Sin filtros globales: un vehiculo borrado conserva sus salidas en el historial.
        var trips = await context.DeliverySessions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Status == SessionStatus.Closed && s.KilometersAtClose != null
                && s.OpenedAt >= fromUtc && s.OpenedAt < toUtc
                // Lo de antes de revisar el km no es confiable; uno que nunca se revisó no cuenta.
                && (s.Vehicle.KilometersCheckedAt != null ? s.OpenedAt >= s.Vehicle.KilometersCheckedAt
                    : !s.Vehicle.KilometersCheckPending && !context.VehicleKilometersReadings.Any(r => r.VehicleId == s.VehicleId)))
            .Select(s => new
            {
                s.VehicleId, s.Vehicle.Name, s.Vehicle.LicensePlate, s.OpenedAt,
                s.KilometersAtOpen, KilometersAtClose = s.KilometersAtClose!.Value,
                Zones = s.Zones.OrderBy(z => z.Position).Select(z => z.Zone.Name).ToList(), MainZone = s.Zone.Name
            })
            .ToListAsync(cancellationToken);
        return trips.Select(t => new SessionKilometersRow(t.VehicleId, t.Name, t.LicensePlate, t.OpenedAt,
            t.KilometersAtOpen, t.KilometersAtClose, t.Zones.Count > 0 ? t.Zones : [t.MainZone])).ToList();
    }
}
