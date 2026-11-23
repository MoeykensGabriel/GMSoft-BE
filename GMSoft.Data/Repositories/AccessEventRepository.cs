using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Access;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class AccessEventRepository(AppDbContext context) : IAccessEventRepository
{
    public async Task AddAsync(AccessEvent accessEvent, CancellationToken ct = default)
        => await context.AccessEvents.AddAsync(accessEvent, ct);

    public Task<bool> HasRejectionAsync(Guid userId, string tokenId, CancellationToken ct = default)
        => context.AccessEvents.AsNoTracking().AnyAsync(e => e.UserId == userId && e.TokenId == tokenId, ct);

    public async Task<IReadOnlyList<AccessEventRow>> GetAsync(DateTime fromUtc, DateTime toUtc, Guid? driverId,
        CancellationToken ct = default)
    {
        // Sin filtros globales: un chofer dado de baja sigue en su historial.
        var events = context.AccessEvents.AsNoTracking()
            .Where(e => driverId == null || e.DriverId == driverId);
        var drivers = context.Drivers.IgnoreQueryFilters().AsNoTracking();
        var inDay = await events.Where(e => e.OccurredAt >= fromUtc && e.OccurredAt < toUtc)
            .Join(drivers, e => e.DriverId, d => d.Id, (e, d) => new AccessEventRow(e.DriverId,
                (d.FirstName + " " + d.LastName).Trim(), e.OccurredAt, e.Kind, e.Reason, e.Client, e.Device, e.DeviceId))
            .ToListAsync(ct);
        if (inDay.Count == 0) return inDay;

        // El último evento de cada chofer antes del día: alcanza con mirar una semana atrás.
        var ids = inDay.Select(e => e.DriverId).Distinct().ToList();
        var since = fromUtc.AddDays(-7);
        var before = await events.Where(e => ids.Contains(e.DriverId) && e.OccurredAt >= since && e.OccurredAt < fromUtc)
            .GroupBy(e => e.DriverId)
            .Select(g => g.OrderByDescending(e => e.OccurredAt).First())
            .ToListAsync(ct);
        var names = inDay.GroupBy(e => e.DriverId).ToDictionary(g => g.Key, g => g.First().DriverName);
        return [.. before.Select(e => new AccessEventRow(e.DriverId, names[e.DriverId], e.OccurredAt, e.Kind, e.Reason,
            e.Client, e.Device, e.DeviceId)), .. inDay];
    }
}
