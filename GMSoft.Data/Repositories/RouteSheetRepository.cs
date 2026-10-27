using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.RouteSheet;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class RouteSheetRepository(AppDbContext context) : IRouteSheetRepository
{
    public async Task<RouteSheetDto?> GetAsync(Guid vehicleId, DateOnly date, int isoDay,
        CancellationToken cancellationToken = default)
    {
        var camion = await context.Vehicles.AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new { v.Name, v.LicensePlate })
            .FirstOrDefaultAsync(cancellationToken);
        if (camion is null) return null;

        // El mismo alcance que la hoja del chofer: camión, día de visita, activo y sin baja pedida.
        var clientes = await context.Customers.AsNoTracking()
            .Where(c => c.VehicleId == vehicleId && c.IsActive
                     && c.VisitDays != null && c.VisitDays.Contains(isoDay)
                     && !c.RemovalRequests.Any(r => r.Status == RemovalStatus.Pending))
            .Select(c => new
            {
                c.Id, c.BusinessName, c.ContactName, c.Address, c.ZoneId, ZoneName = c.Zone.Name, c.RouteOrder,
                Saldo = (context.Deliveries.Where(d => d.CustomerId == c.Id && d.Type != DeliveryType.NoPurchase)
                            .Sum(d => (decimal?)d.Total) ?? 0m)
                      - (context.Payments.Where(p => p.CustomerId == c.Id).Sum(p => (decimal?)p.Amount) ?? 0m),
            })
            .ToListAsync(cancellationToken);

        var ids = clientes.Select(c => c.Id).ToList();
        var porSaldo = await context.CustomerContainerBalances.AsNoTracking()
            .Where(b => ids.Contains(b.CustomerId) && b.Quantity > 0)
            .Select(b => new { b.CustomerId, b.ProductId, b.Product.Detail, b.Quantity })
            .ToListAsync(cancellationToken);
        var porUnidad = await context.ContainerUnits.AsNoTracking()
            .Where(u => u.CurrentCustomerId != null && ids.Contains(u.CurrentCustomerId.Value)
                     && u.Status == ContainerUnitStatus.WithCustomer)
            .GroupBy(u => new { CustomerId = u.CurrentCustomerId!.Value, u.ProductId, u.Product.Detail })
            .Select(g => new { g.Key.CustomerId, g.Key.ProductId, g.Key.Detail, Quantity = g.Count() })
            .ToListAsync(cancellationToken);
        var envases = porSaldo.Concat(porUnidad)
            .GroupBy(e => e.CustomerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RouteSheetContainerDto>)g
                .GroupBy(e => new { e.ProductId, e.Detail })
                .Select(p => new RouteSheetContainerDto(p.Key.ProductId, p.Key.Detail, p.Sum(e => e.Quantity)))
                .OrderByProduct(p => p.ProductDetail).ToList());

        // Las zonas en el orden de la agenda del camión para ese día; las que no figuran, al final.
        var agenda = await context.Set<VehicleScheduleDay>().AsNoTracking()
            .Where(d => d.VehicleId == vehicleId && d.Day == isoDay && d.ZoneId != null)
            .OrderBy(d => d.Position)
            .Select(d => d.ZoneId!.Value)
            .ToListAsync(cancellationToken);

        var zonas = clientes
            .GroupBy(c => new { c.ZoneId, c.ZoneName })
            .OrderBy(g => agenda.IndexOf(g.Key.ZoneId) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(g => g.Key.ZoneName)
            .Select(g => new RouteSheetZoneDto(g.Key.ZoneId, g.Key.ZoneName, g
                .OrderBy(c => c.RouteOrder).ThenBy(c => c.Id)
                .Select(c => new RouteSheetCustomerDto(c.Id, c.RouteOrder,
                    string.IsNullOrWhiteSpace(c.BusinessName) ? c.ContactName : c.BusinessName,
                    c.Address, c.Saldo, envases.GetValueOrDefault(c.Id) ?? []))
                .ToList()))
            .ToList();

        return new RouteSheetDto(vehicleId, camion.Name, camion.LicensePlate, date, zonas);
    }
}
