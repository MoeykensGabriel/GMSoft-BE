using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ContainersOutLineDto>> GetContainersOutAsync(
        CancellationToken cancellationToken = default)
    {
        // Los que se siguen por saldo: suma de lo que tiene cada cliente. Se filtran
        // los saldos en cero y los negativos, que no son envases en la calle.
        var porSaldo = await _context.CustomerContainerBalances
            .AsNoTracking()
            .Where(b => b.Quantity > 0)
            .GroupBy(b => new { b.ProductId, b.Product.Detail, b.Product.Tracking })
            .Select(g => new ContainersOutLineDto(
                g.Key.ProductId,
                g.Key.Detail,
                g.Key.Tracking,
                g.Sum(b => b.Quantity),
                g.Count()))
            .ToListAsync(cancellationToken);

        // Los que se siguen por numero: se cuentan las unidades que estan con un
        // cliente. Para estos productos no hay saldo por cantidad a proposito, asi
        // que este es el unico lugar de donde sale el numero.
        var porUnidad = await _context.ContainerUnits
            .AsNoTracking()
            .Where(u => u.Status == ContainerUnitStatus.WithCustomer)
            .GroupBy(u => new { u.ProductId, u.Product.Detail, u.Product.Tracking })
            .Select(g => new ContainersOutLineDto(
                g.Key.ProductId,
                g.Key.Detail,
                g.Key.Tracking,
                g.Count(),
                g.Select(u => u.CurrentCustomerId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        return porSaldo
            .Concat(porUnidad)
            .OrderByDescending(l => l.QuantityOut)
            .ThenBy(l => l.ProductDetail)
            .ToList();
    }

    public async Task<PagedResult<DebtorLineDto>> GetDebtorsAsync(
        int page,
        int pageSize,
        Guid? zoneId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from c in _context.Customers.AsNoTracking()
            where zoneId == null || c.ZoneId == zoneId
            let vendido = _context.Deliveries
                .Where(d => d.CustomerId == c.Id)
                .Sum(d => (decimal?)d.Total) ?? 0m
            let cobrado = _context.Payments
                .Where(p => p.CustomerId == c.Id)
                .Sum(p => (decimal?)p.Amount) ?? 0m
            where vendido - cobrado > 0
            select new
            {
                Cliente = c,
                Saldo   = vendido - cobrado,
                Ultima  = _context.Deliveries
                    .Where(d => d.CustomerId == c.Id && d.Type == DeliveryType.Sale)
                    .Max(d => (DateTime?)d.DeliveredAt),
                Envases = (_context.CustomerContainerBalances
                              .Where(b => b.CustomerId == c.Id && b.Quantity > 0)
                              .Sum(b => (int?)b.Quantity) ?? 0)
                        + _context.ContainerUnits
                              .Count(u => u.CurrentCustomerId == c.Id
                                       && u.Status == ContainerUnitStatus.WithCustomer),
            };

        var totalCount = await query.CountAsync(cancellationToken);

        var filas = await query
            .OrderByDescending(x => x.Saldo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var hoy = DateTime.UtcNow.Date;

        var items = filas.Select(x => new DebtorLineDto(
            CustomerId:          x.Cliente.Id,
            DisplayName:         string.IsNullOrWhiteSpace(x.Cliente.BusinessName)
                                     ? x.Cliente.ContactName
                                     : x.Cliente.BusinessName,
            Phone:               x.Cliente.Phone,
            Address:             x.Cliente.Address,
            ZoneName:            x.Cliente.Zone == null ? null : x.Cliente.Zone.Name,
            Balance:             x.Saldo,
            LastPurchaseAt:      x.Ultima,
            DaysWithoutPurchase: x.Ultima is null
                                     ? null
                                     : Math.Max(0, (int)(hoy - x.Ultima.Value.Date).TotalDays),
            ContainersHeld:      x.Envases)).ToList();

        return new PagedResult<DebtorLineDto>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<InactiveCustomerLineDto>> GetInactiveCustomersAsync(
        int page,
        int pageSize,
        int days,
        Guid? zoneId,
        CancellationToken cancellationToken = default)
    {
        var corte = DateTime.UtcNow.Date.AddDays(-days);

        var query =
            from c in _context.Customers.AsNoTracking()
            where zoneId == null || c.ZoneId == zoneId
            let ultima = _context.Deliveries
                .Where(d => d.CustomerId == c.Id && d.Type == DeliveryType.Sale)
                .Max(d => (DateTime?)d.DeliveredAt)
            // Nunca compro cuenta como caido: comercialmente es el mismo problema.
            where ultima == null || ultima < corte
            select new
            {
                Cliente = c,
                Ultima  = ultima,
                Saldo   = (_context.Deliveries.Where(d => d.CustomerId == c.Id)
                              .Sum(d => (decimal?)d.Total) ?? 0m)
                        - (_context.Payments.Where(p => p.CustomerId == c.Id)
                              .Sum(p => (decimal?)p.Amount) ?? 0m),
                Envases = (_context.CustomerContainerBalances
                              .Where(b => b.CustomerId == c.Id && b.Quantity > 0)
                              .Sum(b => (int?)b.Quantity) ?? 0)
                        + _context.ContainerUnits
                              .Count(u => u.CurrentCustomerId == c.Id
                                       && u.Status == ContainerUnitStatus.WithCustomer),
            };

        var totalCount = await query.CountAsync(cancellationToken);

        // Nulos primero seria poner arriba a los que nunca compraron, que suelen ser
        // altas recientes. Ordenar por fecha ascendente con los nulos al final deja
        // arriba al que hace mas tiempo que no compra, que es a quien hay que llamar.
        var filas = await query
            .OrderBy(x => x.Ultima == null)
            .ThenBy(x => x.Ultima)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var hoy = DateTime.UtcNow.Date;

        var items = filas.Select(x => new InactiveCustomerLineDto(
            CustomerId:          x.Cliente.Id,
            DisplayName:         string.IsNullOrWhiteSpace(x.Cliente.BusinessName)
                                     ? x.Cliente.ContactName
                                     : x.Cliente.BusinessName,
            Phone:               x.Cliente.Phone,
            Address:             x.Cliente.Address,
            ZoneName:            x.Cliente.Zone == null ? null : x.Cliente.Zone.Name,
            LastPurchaseAt:      x.Ultima,
            DaysWithoutPurchase: x.Ultima is null
                                     ? null
                                     : Math.Max(0, (int)(hoy - x.Ultima.Value.Date).TotalDays),
            Balance:             x.Saldo,
            ContainersHeld:      x.Envases)).ToList();

        return new PagedResult<InactiveCustomerLineDto>(items, totalCount, page, pageSize);
    }
}
