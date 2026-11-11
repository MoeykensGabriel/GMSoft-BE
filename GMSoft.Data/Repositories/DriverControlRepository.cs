using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.DriverControl;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class DriverControlRepository(AppDbContext context) : IDriverControlRepository
{
    public async Task<(IReadOnlyList<DriverControlSessionRow> Sessions, IReadOnlyList<DriverControlVisitRow> Visits)> GetAsync(
        DateTime fromUtc, DateTime toUtc, Guid? driverId, CancellationToken cancellationToken = default)
    {
        // Sin filtros globales: un chofer, camión o cliente dado de baja sigue en su historial.
        var selected = context.DeliverySessions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Status != SessionStatus.Cancelled && s.OpenedAt >= fromUtc && s.OpenedAt < toUtc
                && (driverId == null || s.DriverId == driverId));
        var sessions = await selected
            .Select(s => new DriverControlSessionRow(s.Id, s.DriverId,
                (s.Driver.FirstName + " " + s.Driver.LastName).Trim(), s.Vehicle.Name, s.Vehicle.LicensePlate,
                s.Zone.Name, s.OpenedAt, s.ClosedAt, s.Status, s.OpenedOutsideWorkingHours,
                s.InitialLoads.Where(l => !l.IsDeleted).Min(l => (DateTime?)l.LoadedAt)))
            .ToListAsync(cancellationToken);
        var visits = await context.Deliveries.IgnoreQueryFilters().AsNoTracking()
            .Where(d => !d.IsDeleted && selected.Any(s => s.Id == d.DeliverySessionId))
            .Select(d => new DriverControlVisitRow(d.DeliverySessionId, d.Customer.BusinessName ?? d.Customer.ContactName,
                d.Type, d.NoPurchaseReason, d.DeliveredAt, d.MarkedAt, d.MarkedAtDoubtful, d.IsOutsideWorkingHours))
            .ToListAsync(cancellationToken);
        return (sessions, visits);
    }
}
