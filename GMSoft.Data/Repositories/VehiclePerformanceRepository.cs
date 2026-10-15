using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.VehiclePerformance;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehiclePerformanceRepository(AppDbContext context) : IVehiclePerformanceRepository
{
    public async Task<IReadOnlyList<VehiclePerformanceLineDto>> GetAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var selected = context.DeliverySessions.AsNoTracking()
            .Where(s => s.OpenedAt >= fromUtc && s.OpenedAt < toUtc);
        // Keep historical vehicle/driver identities even if they were subsequently deleted.
        var sessions = await selected.IgnoreQueryFilters().Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.VehicleId, s.DriverId,
                DriverName = s.Driver.FirstName + " " + s.Driver.LastName, s.Status,
                s.KilometersAtOpen, s.KilometersAtClose,
                Received = s.CashSettlement != null && !s.CashSettlement.IsDeleted
                    ? (decimal?)s.CashSettlement.AmountReceived : null })
            .ToListAsync(cancellationToken);
        var sessionLookup = sessions.ToLookup(s => s.VehicleId);
        var vehicles = await context.Vehicles.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted || selected.Any(s => s.VehicleId == v.Id))
            .Select(v => new { v.Id, v.Name, v.LicensePlate }).ToListAsync(cancellationToken);
        var routes = await context.Customers.Where(c => c.IsActive && c.VehicleId != null)
            .GroupBy(c => c.VehicleId!.Value).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var deliveries = context.Deliveries.Where(d => selected.Any(s => s.Id == d.DeliverySessionId));
        var visits = await deliveries.GroupBy(d => d.DeliverySession.VehicleId)
            .Select(g => new { Id = g.Key, Visits = g.Count(), Visited = g.Select(d => d.CustomerId).Distinct().Count(),
                Buyers = g.Where(d => d.Type == DeliveryType.Sale).Select(d => d.CustomerId).Distinct().Count(),
                Sales = g.Count(d => d.Type == DeliveryType.Sale),
                Sold = g.Where(d => d.Type == DeliveryType.Sale).Sum(d => d.Total) })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var reasons = (await deliveries.Where(d => d.Type == DeliveryType.NoPurchase)
            .GroupBy(d => new { d.DeliverySession.VehicleId, d.NoPurchaseReason })
            .Select(g => new { g.Key.VehicleId, Reason = g.Key.NoPurchaseReason, Count = g.Count() })
            .ToListAsync(cancellationToken)).ToLookup(x => x.VehicleId);
        var products = (await context.DeliveryItems.Where(i => i.Delivery.Type == DeliveryType.Sale &&
                selected.Any(s => s.Id == i.Delivery.DeliverySessionId))
            .GroupBy(i => new { i.Delivery.DeliverySession.VehicleId, i.ProductId, i.Product.Detail })
            .Select(g => new { g.Key.VehicleId, g.Key.ProductId, g.Key.Detail,
                Units = g.Sum(i => (long)i.Quantity), Amount = g.Sum(i => i.Quantity * i.UnitPrice) })
            .ToListAsync(cancellationToken)).ToLookup(x => x.VehicleId);
        var payments = (await context.Payments.Where(p => selected.Any(s => s.Id == p.DeliverySessionId))
            .GroupBy(p => new { p.DeliverySessionId, p.Method })
            .Select(g => new { SessionId = g.Key.DeliverySessionId!.Value, g.Key.Method, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken)).ToLookup(x => x.SessionId);
        // The signed ledger balance equals loaded - delivered - returned at reception,
        // including adjustments, transfers and promotions (same identity as DailySummary).
        var stock = (await context.SessionStockMovements.Where(m => selected.Any(s =>
                s.Id == m.DeliverySessionId && s.Status == SessionStatus.Closed))
            .GroupBy(m => new { m.DeliverySession.VehicleId, m.State })
            .Select(g => new { g.Key.VehicleId, g.Key.State, Quantity = g.Sum(m => (long)m.Quantity) })
            .ToListAsync(cancellationToken)).ToLookup(x => x.VehicleId);

        return vehicles.Select(v =>
        {
            var trips = sessionLookup[v.Id].ToArray();
            var paid = trips.SelectMany(s => payments[s.Id]).ToArray();
            decimal Paid(PaymentMethod method) => paid.Where(p => p.Method == method).Sum(p => p.Amount);
            visits.TryGetValue(v.Id, out var activity);
            var byReason = reasons[v.Id].ToDictionary(r => r.Reason?.ToString() ?? "Unknown", r => r.Count);
            return new VehiclePerformanceLineDto(v.Id, v.Name, v.LicensePlate,
                trips.Select(s => s.DriverName.Trim()).Distinct().OrderBy(n => n).ToArray(),
                trips.Length, trips.Count(s => s.Status == SessionStatus.Closed), activity?.Sold ?? 0, paid.Sum(p => p.Amount),
                new(Paid(PaymentMethod.Cash), Paid(PaymentMethod.Transfer), Paid(PaymentMethod.Card)),
                routes.GetValueOrDefault(v.Id), activity?.Visited ?? 0, activity?.Buyers ?? 0,
                activity?.Visits ?? 0, activity?.Sales ?? 0, new(byReason.Values.Sum(), byReason),
                products[v.Id].OrderBy(p => p.Detail).ThenBy(p => p.ProductId)
                    .Select(p => new ProductPerformanceDto(p.ProductId, p.Detail, p.Units, p.Amount)).ToArray(),
                new(stock[v.Id].Where(s => s.State == ContainerState.Full).Sum(s => s.Quantity),
                    stock[v.Id].Where(s => s.State == ContainerState.Empty).Sum(s => s.Quantity)),
                trips.Sum(s => CashSettlementCalculation.Difference(
                    payments[s.Id].Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), s.Received) ?? 0),
                trips.Where(s => s.Status == SessionStatus.Closed && s.KilometersAtClose.HasValue)
                    .Sum(s => (long)s.KilometersAtClose!.Value - s.KilometersAtOpen));
        }).OrderByDescending(v => v.Sold).ThenBy(v => v.VehicleName).ThenBy(v => v.VehicleId).ToArray();
    }
}
