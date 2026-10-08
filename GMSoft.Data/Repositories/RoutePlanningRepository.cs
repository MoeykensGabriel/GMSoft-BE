using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class RoutePlanningRepository(AppDbContext context) : IRoutePlanningRepository
{
    public async Task LockCustomersAsync(CancellationToken ct)
    {
        // Covers INSERT phantoms and writers that do not use advisory locks (street sales,
        // individual edits, promotions). Reads remain available; released at commit/rollback.
        await context.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"Customers\" IN SHARE ROW EXCLUSIVE MODE", ct);
        // A retry of the transaction must read persisted values, not the previous attempt's entities.
        context.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<Customer>> GetRouteAsync(Guid vehicleId, Guid zoneId, int day, CancellationToken ct)
        => await context.Customers.Include(c => c.Zone).Include(c => c.Vehicle)
            .Where(c => c.IsActive && c.VehicleId == vehicleId && c.ZoneId == zoneId &&
                c.VisitDays != null && c.VisitDays.Contains(day))
            .OrderBy(c => c.RouteOrder).ThenBy(c => c.Id).ToListAsync(ct);
}
