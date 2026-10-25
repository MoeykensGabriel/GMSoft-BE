using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
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
            .Include(s => s.Zones).ThenInclude(z => z.Zone)
            .Include(s => s.CashSettlement)
            .Include(s => s.InitialLoads)
            .Include(s => s.Deliveries)
            .Include(s => s.Promotions)
            .Include(s => s.ClosedPromotions)
            .Include(s => s.Restocks)
            .AsSplitQuery()
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
        // Lo que el cliente devolvió por encima de lo que tenía registrado: la anotación de la visita.
        var recovered = await context.ContainerMovements.AsNoTracking()
            .Where(m => m.Type == ContainerMovementType.ExchangeCorrection && m.Delivery != null
                && ids.Contains(m.Delivery.DeliverySessionId))
            .GroupBy(m => new { m.Delivery!.DeliverySessionId, m.ProductId })
            .Select(g => new DailySummaryRecovered(g.Key.DeliverySessionId, g.Key.ProductId, g.Sum(m => m.Quantity)))
            .ToListAsync(cancellationToken);
        return new(sessions, payments) { Recovered = recovered };
    }
}
