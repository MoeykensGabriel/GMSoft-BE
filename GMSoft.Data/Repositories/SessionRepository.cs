using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class SessionRepository : Repository<DeliverySession>, ISessionRepository
{
    public SessionRepository(AppDbContext context) : base(context) { }

    public async Task<DeliverySession?> GetOpenByDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .FirstOrDefaultAsync(
                s => s.DriverId == driverId && s.Status == SessionStatus.Open,
                cancellationToken);

    public async Task<bool> HasOpenSessionForVehicleAsync(
        Guid vehicleId,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .AsNoTracking()
            .AnyAsync(s =>
                s.VehicleId == vehicleId &&
                s.Status == SessionStatus.Open &&
                (excludeSessionId == null || s.Id != excludeSessionId),
                cancellationToken);

    public async Task<DeliverySession?> GetWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .AsNoTracking()
            .Include(s => s.Driver)
            .Include(s => s.Vehicle)
            .Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <summary>
    /// El stock a bordo se calcula sumando los movimientos, no se guarda. Asi no hay
    /// dos numeros para lo mismo, y una sesion cerrada con saldo distinto de cero es
    /// exactamente el faltante.
    /// </summary>
    public async Task<IReadOnlyList<SessionStockLineDto>> GetStockBalanceAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var porProducto = await _context.SessionStockMovements
            .AsNoTracking()
            .Where(m => m.DeliverySessionId == sessionId)
            .GroupBy(m => new { m.ProductId, Detalle = m.Product.Detail })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Detalle,
                Full  = g.Where(m => m.State == ContainerState.Full).Sum(m => (int?)m.Quantity) ?? 0,
                Empty = g.Where(m => m.State == ContainerState.Empty).Sum(m => (int?)m.Quantity) ?? 0
            })
            .ToListAsync(cancellationToken);

        return porProducto
            .Select(x => new SessionStockLineDto(x.ProductId, x.Detalle, x.Full, x.Empty))
            .OrderBy(x => x.ProductDetail)
            .ToList();
    }

    public async Task<IReadOnlyList<SessionDeliveryDto>> GetDeliveriesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await _context.Deliveries
            .AsNoTracking()
            .Where(d => d.DeliverySessionId == sessionId)
            .OrderBy(d => d.DeliveredAt)
            .Select(d => new SessionDeliveryDto(
                d.Id,
                d.CustomerId,
                d.Customer.BusinessName ?? d.Customer.ContactName,
                d.Customer.Address,
                d.Type,
                d.DeliveredAt,
                d.Total,
                d.Notes,
                d.Items.Select(i => new SessionDeliveryItemDto(
                    i.ProductId, i.Product.Detail, i.Quantity, i.UnitPrice)).ToList(),
                d.ContainerMovements.Select(m => new SessionDeliveryContainerDto(
                    m.ProductId, m.Product.Detail, m.Quantity)).ToList()))
            .ToListAsync(cancellationToken);

    public async Task<(decimal Sold, decimal Collected)> GetMoneyTotalsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var vendido = await _context.Deliveries
            .AsNoTracking()
            .Where(d => d.DeliverySessionId == sessionId)
            .SumAsync(d => (decimal?)d.Total, cancellationToken) ?? 0m;

        var cobrado = await _context.Payments
            .AsNoTracking()
            .Where(p => p.DeliverySessionId == sessionId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return (vendido, cobrado);
    }

    public async Task<SessionCashSettlement?> GetSettlementAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await _context.SessionCashSettlements
            .FirstOrDefaultAsync(s => s.DeliverySessionId == sessionId, cancellationToken);

    public async Task<(IReadOnlyList<DeliverySession> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? driverId,
        Guid? zoneId,
        Guid? vehicleId,
        DateTime? openedFromUtc,
        DateTime? openedToUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DeliverySessions
            .AsNoTracking()
            .Include(s => s.Driver)
            .Include(s => s.Vehicle)
            .Include(s => s.Zone)
            .AsQueryable();

        if (driverId is not null)
            query = query.Where(s => s.DriverId == driverId.Value);

        if (zoneId is not null)
            query = query.Where(s => s.ZoneId == zoneId.Value);

        if (vehicleId is not null)
            query = query.Where(s => s.VehicleId == vehicleId.Value);

        // Se filtra por cuando SALIO. Una salida que cerro despues de medianoche
        // sigue siendo el reparto del dia en que arranco.
        if (openedFromUtc is not null)
            query = query.Where(s => s.OpenedAt >= openedFromUtc.Value);

        if (openedToUtc is not null)
            query = query.Where(s => s.OpenedAt < openedToUtc.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
