using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Vehicles.Schedule;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleScheduleRepository(AppDbContext db) : IVehicleScheduleRepository
{
    public async Task<IReadOnlyList<ScheduleDayDto>> GetAsync(Guid vehicleId, CancellationToken ct)
    {
        var rows = await db.Set<VehicleScheduleDay>().AsNoTracking().Where(d => d.VehicleId == vehicleId)
            .OrderBy(d => d.Day).ThenBy(d => d.Position)
            .Select(d => new { d.Day, d.ZoneId, ZoneName = d.Zone == null ? null : d.Zone.Name }).ToListAsync(ct);
        // Una fila por zona del día: se juntan en orden; la primera queda como la zona del día.
        return rows.GroupBy(d => d.Day).Select(g =>
        {
            var zones = g.Where(d => d.ZoneId is not null).Select(d => new ScheduleZoneDto(d.ZoneId!.Value, d.ZoneName ?? string.Empty)).ToList();
            return new ScheduleDayDto(g.Key, zones.FirstOrDefault()?.ZoneId, zones.FirstOrDefault()?.ZoneName) { Zones = zones };
        }).ToList();
    }

    public async Task ReplaceAsync(Guid vehicleId, ScheduleDayInput[] days, CancellationToken ct)
    {
        // Caller holds the vehicle row lock, including when this is the first saved week.
        await db.Set<VehicleScheduleDay>().Where(d => d.VehicleId == vehicleId).ExecuteDeleteAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<VehicleScheduleDay>()
                     .Where(e => e.Entity.VehicleId == vehicleId).ToArray())
            entry.State = EntityState.Detached;
        // Un día sin zonas es una fila sin zona: la semana guardada siempre tiene los siete días.
        db.Set<VehicleScheduleDay>().AddRange(days.SelectMany(d => d.EffectiveZoneIds.Length == 0
            ? [new VehicleScheduleDay { VehicleId = vehicleId, Day = d.Day, Position = 0 }]
            : d.EffectiveZoneIds.Select((zoneId, i) => new VehicleScheduleDay { VehicleId = vehicleId, Day = d.Day, Position = i, ZoneId = zoneId })));
    }
    public async Task<IReadOnlyList<ScheduleHistory>> HistoryAsync(Guid vehicleId, DateTime from, DateTime to, CancellationToken ct) =>
        await db.DeliverySessions.AsNoTracking().Where(s => s.VehicleId == vehicleId && s.OpenedAt >= from && s.OpenedAt <= to && s.Zone.IsActive)
            .Select(s => new ScheduleHistory(s.ZoneId, s.Zone.Name, s.OpenedAt, s.RouteDays)).ToListAsync(ct);

    public async Task<PagedResult<UncoveredCustomerDto>> UncoveredAsync(GetUncoveredQuery r, CancellationToken ct)
    {
        var schedule = db.Set<VehicleScheduleDay>();
        var query = db.Customers.AsNoTracking().Where(c => c.IsActive && !c.RemovalRequests.Any(r => r.Status == RemovalStatus.Pending) && c.VehicleId != null &&
            (!r.VehicleId.HasValue || c.VehicleId == r.VehicleId) &&
            schedule.Any(d => d.VehicleId == c.VehicleId) && c.VisitDays != null &&
            c.VisitDays.Any(day => !schedule.Any(d => d.VehicleId == c.VehicleId && d.Day == day && d.ZoneId == c.ZoneId)));
        var count = await query.CountAsync(ct);
        var customers = await query.OrderBy(c => c.Id).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(c => new { c.Id, c.BusinessName, c.ContactName, c.Address, c.VehicleId, c.ZoneId, ZoneName = c.Zone.Name, c.VisitDays }).ToListAsync(ct);
        var ids = customers.Select(c => c.VehicleId!.Value).Distinct().ToArray();
        var weeks = await schedule.AsNoTracking().Where(d => ids.Contains(d.VehicleId)).ToListAsync(ct);
        var items = customers.Select(c => new UncoveredCustomerDto(c.Id,
            string.IsNullOrWhiteSpace(c.BusinessName) ? c.ContactName : c.BusinessName, c.Address, c.VehicleId!.Value,
            c.ZoneId, c.ZoneName, c.VisitDays!, c.VisitDays!.Where(day => !weeks.Any(d => d.VehicleId == c.VehicleId && d.Day == day && d.ZoneId == c.ZoneId)).Order().ToArray())).ToArray();
        return new(items, count, r.Page, r.PageSize);
    }
}
