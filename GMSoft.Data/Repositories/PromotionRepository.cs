using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class PromotionRepository(AppDbContext context) : Repository<Promotion>(context), IPromotionRepository
{
    private IQueryable<Promotion> Details => _context.Set<Promotion>()
        .Include(p => p.Vehicle).Include(p => p.Driver).Include(p => p.Lines).ThenInclude(l => l.Product);

    public async Task LockAsync(string key, CancellationToken ct) =>
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

    public Task<Promotion?> FindRequestAsync(Guid requestId, bool closing, CancellationToken ct) =>
        _context.Set<Promotion>().Include(p => p.Lines).FirstOrDefaultAsync(
            p => closing ? p.CloseClientRequestId == requestId : p.ClientRequestId == requestId, ct);

    public Task<Promotion?> GetDetailsAsync(Guid id, CancellationToken ct) =>
        Details.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Promotion>> GetPendingAsync(Guid vehicleId, CancellationToken ct) =>
        await Details.AsNoTracking().Where(p => p.VehicleId == vehicleId && p.Status == PromotionStatus.Pending)
            .OrderBy(p => p.PickupDate).ThenBy(p => p.RegisteredAt).ThenBy(p => p.Id).ToListAsync(ct);

    public async Task<(IReadOnlyList<Promotion> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? status, Guid? vehicleId, DateTime? fromUtc, DateTime? toUtc,
        DateOnly today, CancellationToken ct)
    {
        var q = Details.AsNoTracking();
        if (vehicleId.HasValue) q = q.Where(p => p.VehicleId == vehicleId);
        if (fromUtc.HasValue) q = q.Where(p => p.RegisteredAt >= fromUtc);
        if (toUtc.HasValue) q = q.Where(p => p.RegisteredAt < toUtc);
        q = status switch
        {
            "Overdue" => q.Where(p => p.Status == PromotionStatus.Pending && p.PickupDate < today),
            "Pending" => q.Where(p => p.Status == PromotionStatus.Pending),
            "Converted" => q.Where(p => p.Status == PromotionStatus.Converted),
            "NotConverted" => q.Where(p => p.Status == PromotionStatus.NotConverted),
            _ => q
        };
        var count = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.RegisteredAt).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, count);
    }
}

public class PromotionSettingsRepository(AppDbContext context) : IPromotionSettingsRepository
{
    public async Task<int> GetPickupDaysAsync(CancellationToken ct)
    {
        // Inicializacion perezosa y atomica: la migracion solo crea tablas/indices.
        await context.Database.ExecuteSqlRawAsync(
            """INSERT INTO "PromotionSettings" ("Id", "PickupDays") VALUES (1, 7) ON CONFLICT ("Id") DO NOTHING""", ct);
        return await context.Set<PromotionSettings>().Where(p => p.Id == 1).Select(p => p.PickupDays).SingleAsync(ct);
    }

    public async Task SetPickupDaysAsync(int days, CancellationToken ct) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "PromotionSettings" ("Id", "PickupDays") VALUES (1, {days}) ON CONFLICT ("Id") DO UPDATE SET "PickupDays" = EXCLUDED."PickupDays" """, ct);
}

