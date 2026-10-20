using GMSoft.Application.Features.Sessions.Cancel;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class SessionCancellationRepository(AppDbContext db) : ISessionCancellationRepository
{
    public async Task<bool> HasActivityAsync(Guid sessionId, CancellationToken ct)
        => await db.Deliveries.AnyAsync(d => d.DeliverySessionId == sessionId, ct)
        || await db.Payments.AnyAsync(p => p.DeliverySessionId == sessionId, ct)
        || await db.SessionRestocks.AnyAsync(r => r.DeliverySessionId == sessionId, ct)
        || await db.Promotions.AnyAsync(p => p.DeliverySessionId == sessionId || p.ClosingSessionId == sessionId, ct)
        || await db.SessionStockMovements.AnyAsync(m => m.DeliverySessionId == sessionId
            && m.Type != SessionStockMovementType.InitialLoad, ct);

    public Task<int?> GetLastClosedKilometersAsync(Guid vehicleId, Guid excludeSessionId, CancellationToken ct)
        => db.DeliverySessions.AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && s.Id != excludeSessionId
                && s.Status == SessionStatus.Closed && s.KilometersAtClose != null)
            .OrderByDescending(s => s.ClosedAt)
            .Select(s => s.KilometersAtClose)
            .FirstOrDefaultAsync(ct);

    public async Task ReleaseLoadsAsync(Guid sessionId, CancellationToken ct)
    {
        // Sobre las entidades trackeadas: así quedan en la misma transacción que la anulación.
        var cargas = await db.VehicleLoads.Where(l => l.ConsumedBySessionId == sessionId).ToListAsync(ct);
        foreach (var carga in cargas)
            carga.ConsumedBySessionId = null;
    }
}
