using GMSoft.Application.Features.Sessions.Correction;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ReceptionCorrectionRepository(AppDbContext db) : IReceptionCorrectionRepository
{
    public async Task<IReadOnlyList<ReturnedQuantity>> GetReturnedAsync(Guid sessionId, CancellationToken ct)
        => await db.SessionStockMovements.AsNoTracking()
            .Where(m => m.DeliverySessionId == sessionId && m.Type == SessionStockMovementType.ReturnedAtClose)
            .GroupBy(m => new { m.ProductId, m.State })
            // La descarga sale del camion, asi que se guarda en negativo.
            .Select(g => new ReturnedQuantity(g.Key.ProductId, g.Key.State, -g.Sum(m => m.Quantity)))
            .ToListAsync(ct);

    public async Task<int?> GetNextOpeningKilometersAsync(Guid vehicleId, Guid sessionId, DateTime openedAt, CancellationToken ct)
        => await db.DeliverySessions.AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && s.Id != sessionId && s.OpenedAt > openedAt)
            .OrderBy(s => s.OpenedAt)
            .Select(s => (int?)s.KilometersAtOpen)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SessionReceptionCorrection>> GetHistoryAsync(Guid sessionId, CancellationToken ct)
        => await db.SessionReceptionCorrections.AsNoTracking()
            .Include(c => c.Lines).ThenInclude(l => l.Product)
            .Where(c => c.DeliverySessionId == sessionId)
            .OrderBy(c => c.CorrectedAt).ThenBy(c => c.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task AddAsync(SessionReceptionCorrection correction, CancellationToken ct)
        => await db.SessionReceptionCorrections.AddAsync(correction, ct);
}
