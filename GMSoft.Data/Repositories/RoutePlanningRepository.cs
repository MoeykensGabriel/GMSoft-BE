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
        => await GetRouteAsync(vehicleId, zoneId, [day], ct);

    public async Task<IReadOnlyList<Customer>> GetRouteAsync(Guid vehicleId, Guid zoneId, int[] days, CancellationToken ct)
        => await context.Customers.Include(c => c.Zone).Include(c => c.Vehicle)
            .Where(c => c.IsActive && !c.RemovalRequests.Any(r => r.Status == RemovalStatus.Pending) && c.VehicleId == vehicleId && c.ZoneId == zoneId &&
                c.VisitDays != null && c.VisitDays.Any(day => days.Contains(day)))
            .OrderBy(c => c.RouteOrder).ThenBy(c => c.Id).ToListAsync(ct);

    public async Task AddChangeAsync(RouteOrderChange change, CancellationToken ct)
        => await context.RouteOrderChanges.AddAsync(change, ct);

    public Task<RouteOrderChange?> GetLastChangeAsync(Guid vehicleId, Guid zoneId, CancellationToken ct, Guid? sessionId = null)
        => context.RouteOrderChanges.AsNoTracking()
            // De una salida vale el cambio en cualquiera de sus zonas; si no, el de la zona pedida.
            .Where(c => c.VehicleId == vehicleId &&
                (sessionId == null ? c.ZoneId == zoneId : c.DeliverySessionId == sessionId))
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id).FirstOrDefaultAsync(ct);
}
