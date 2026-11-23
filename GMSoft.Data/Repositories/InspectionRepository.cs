using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Inspections;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class InspectionRepository(AppDbContext context) : IInspectionRepository
{
    public async Task<IReadOnlyList<InspectionDepartureDto>> GetOpenDeparturesAsync(Guid? sessionId, CancellationToken ct)
    {
        var rows = await context.DeliverySessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Open && (sessionId == null || s.Id == sessionId))
            .OrderBy(s => s.Vehicle.Name)
            .Select(s => new
            {
                s.Id, s.Vehicle.Name, s.Vehicle.LicensePlate,
                Driver = (s.Driver.FirstName + " " + s.Driver.LastName).Trim(), s.OpenedAt,
                Zones = s.Zones.OrderBy(z => z.Position).Select(z => z.Zone.Name).ToList(),
                Main = s.Zone.Name,
            })
            .ToListAsync(ct);
        return rows.Select(r => new InspectionDepartureDto(r.Id, r.Name, r.LicensePlate, r.Driver, r.OpenedAt,
            r.Zones.Count > 0 ? r.Zones : [r.Main])).ToList();
    }

    public async Task<IReadOnlyList<BalanceProduct>> GetBalanceProductsAsync(CancellationToken ct)
        => await context.Products.AsNoTracking()
            .Where(p => p.Tracking == ContainerTracking.ByBalance && p.IsPublished)
            .OrderBy(p => p.Detail)
            .Select(p => new BalanceProduct(p.Id, p.Detail))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<(Guid CustomerId, Guid ProductId), int>> GetBalancesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken ct)
    {
        if (customerIds.Count == 0) return new Dictionary<(Guid, Guid), int>();
        var rows = await context.CustomerContainerBalances.AsNoTracking()
            .Where(b => customerIds.Contains(b.CustomerId))
            .Select(b => new { b.CustomerId, b.ProductId, b.Quantity })
            .ToListAsync(ct);
        return rows.ToDictionary(r => (r.CustomerId, r.ProductId), r => r.Quantity);
    }

    public async Task<IReadOnlyDictionary<Guid, CustomerVisitRow>> GetDriverVisitsAsync(Guid sessionId, CancellationToken ct)
    {
        var rows = await context.Deliveries.AsNoTracking()
            .Where(d => d.DeliverySessionId == sessionId)
            .Select(d => new CustomerVisitRow(d.CustomerId, d.Type, d.NoPurchaseReason, d.DeliveredAt))
            .ToListAsync(ct);
        // Si pasó dos veces, vale lo último que marcó.
        return rows.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key, g => g.MaxBy(r => r.At)!);
    }

    public async Task<IReadOnlyList<CustomerInspection>> GetBySessionAsync(Guid sessionId, CancellationToken ct)
        => await context.CustomerInspections.AsNoTracking()
            .Where(i => i.DeliverySessionId == sessionId)
            .ToListAsync(ct);

    public Task<CustomerInspection?> GetAsync(Guid sessionId, Guid customerId, CancellationToken ct)
        => context.CustomerInspections.FirstOrDefaultAsync(i => i.DeliverySessionId == sessionId && i.CustomerId == customerId, ct);

    public async Task AddAsync(CustomerInspection inspection, CancellationToken ct)
        => await context.CustomerInspections.AddAsync(inspection, ct);

    public Task ConfirmPurchaseAsync(Guid customerId, DateTime at, CancellationToken ct)
        => context.Customers.Where(c => c.Id == customerId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PurchaseConfirmedAt, at), ct);
}

public class InspectionReportRepository(AppDbContext context) : IInspectionReportRepository
{
    public async Task<PagedResult<InspectionReportDto>> GetAsync(GetInspectionReportQuery q, CancellationToken ct)
    {
        // Sin filtros globales: un cliente o un controlador dado de baja sigue en el historial.
        var query = context.CustomerInspections.IgnoreQueryFilters().AsNoTracking()
            .Where(i => !i.IsDeleted
                     && (q.InspectorId == null || i.InspectorId == q.InspectorId)
                     && (q.VehicleId == null || i.DeliverySession.VehicleId == q.VehicleId));
        var total = await query.CountAsync(ct);
        var rows = await query
            // Por salida, la más reciente primero; adentro, en orden de recorrido: zona por zona
            // en el orden en que la salida las recorre, y en cada zona por RouteOrder.
            .OrderByDescending(i => i.DeliverySession.OpenedAt).ThenBy(i => i.DeliverySessionId)
            .ThenBy(i => i.DeliverySession.Zones.Where(z => z.ZoneId == i.Customer.ZoneId).Select(z => (int?)z.Position).FirstOrDefault() ?? 0)
            .ThenBy(i => i.Customer.RouteOrder).ThenBy(i => i.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(i => new
            {
                i.Id, i.InspectedAt, i.DeliverySessionId, i.CustomerId, SessionOpenedAt = i.DeliverySession.OpenedAt, i.Customer.RouteOrder,
                Inspector = (i.Inspector.FirstName + " " + i.Inspector.LastName).Trim(),
                Vehicle = i.DeliverySession.Vehicle.Name, i.DeliverySession.Vehicle.LicensePlate,
                Driver = (i.DeliverySession.Driver.FirstName + " " + i.DeliverySession.Driver.LastName).Trim(),
                i.Customer.BusinessName, i.Customer.ContactName, i.Customer.Address, Zone = i.Customer.Zone.Name,
                i.StillBuys, i.NotBuyingReason, i.Notes, i.Containers,
            })
            .ToListAsync(ct);

        var productIds = rows.SelectMany(r => r.Containers.Select(c => c.ProductId)).Distinct().ToList();
        var products = await context.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Detail, ct);

        // Lo que marcó el chofer en esa salida para esos clientes: la última visita de cada uno.
        var sessionIds = rows.Select(r => r.DeliverySessionId).Distinct().ToList();
        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var visits = (await context.Deliveries.AsNoTracking()
                .Where(d => sessionIds.Contains(d.DeliverySessionId) && customerIds.Contains(d.CustomerId))
                .Select(d => new { d.DeliverySessionId, d.CustomerId, d.Type, d.NoPurchaseReason, d.DeliveredAt })
                .ToListAsync(ct))
            .GroupBy(d => (d.DeliverySessionId, d.CustomerId))
            .ToDictionary(g => g.Key, g => g.MaxBy(d => d.DeliveredAt)!);

        var items = rows.Select(r => new InspectionReportDto(r.Id, r.DeliverySessionId, r.SessionOpenedAt, r.RouteOrder, r.InspectedAt, r.Inspector, r.Vehicle, r.LicensePlate,
            r.Driver, r.CustomerId, string.IsNullOrWhiteSpace(r.BusinessName) ? r.ContactName : r.BusinessName, r.Address, r.Zone,
            visits.TryGetValue((r.DeliverySessionId, r.CustomerId), out var v) ? new DriverVisitDto(v.Type, v.NoPurchaseReason, v.DeliveredAt) : null,
            r.StillBuys, r.NotBuyingReason, r.Notes,
            r.Containers.Select(c => new InspectionReportCountDto(products.GetValueOrDefault(c.ProductId, "Producto"),
                c.SystemQuantity, c.RealQuantity)).OrderBy(c => c.Product).ToList())).ToList();
        return new PagedResult<InspectionReportDto>(items, total, q.Page, q.PageSize);
    }
}
