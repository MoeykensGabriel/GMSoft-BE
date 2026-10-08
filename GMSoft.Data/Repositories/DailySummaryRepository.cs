using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class DailySummaryRepository(AppDbContext context) : IDailySummaryRepository
{
    public async Task<DailySummaryData> GetAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var sessions = await context.DeliverySessions.AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && s.OpenedAt >= fromUtc && s.OpenedAt < toUtc)
            .Include(s => s.Driver).Include(s => s.Vehicle).Include(s => s.Zone)
            .Include(s => s.CashSettlement)
            .Include(s => s.StockMovements).ThenInclude(m => m.Product)
            .OrderBy(s => s.OpenedAt).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0) return new(sessions, []);

        var ids = sessions.Select(s => s.Id).ToArray();
        var payments = await context.Payments.AsNoTracking()
            .Where(p => p.DeliverySessionId.HasValue && ids.Contains(p.DeliverySessionId.Value))
            .GroupBy(p => new { SessionId = p.DeliverySessionId!.Value, p.Method })
            .Select(g => new DailySummaryPayment(g.Key.SessionId, g.Key.Method, g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);
        return new(sessions, payments);
    }
}
