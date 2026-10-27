using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class SessionRepository : Repository<DeliverySession>, ISessionRepository
{
    public SessionRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<DeliverySession>> GetOpenWithStockAsync(
        CancellationToken cancellationToken = default)
        => await _context.DeliverySessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Open)
            .Include(s => s.Driver).Include(s => s.Vehicle).Include(s => s.Zone)
            .Include(s => s.Zones).ThenInclude(z => z.Zone)
            .Include(s => s.StockMovements).ThenInclude(m => m.Product)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<DeliverySession?> GetOpenByDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
        // Con sus zonas: decide qué clientes puede atender el chofer.
        => await _context.DeliverySessions
            .Include(s => s.Zones)
            .FirstOrDefaultAsync(
                s => s.DriverId == driverId && s.Status == SessionStatus.Open,
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetZoneIdsAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => await _context.DeliverySessionZones.AsNoTracking()
            .Where(z => z.DeliverySessionId == sessionId)
            .OrderBy(z => z.Position)
            .Select(z => z.ZoneId)
            .ToListAsync(cancellationToken);

    public Task<DeliverySession?> GetByIdIncludingCancelledAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.DeliverySessions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && (!s.IsDeleted || s.Status == SessionStatus.Cancelled), cancellationToken);

    public async Task<DeliverySession?> LockAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.DeliverySessions
            .FromSqlInterpolated($"SELECT * FROM \"DeliverySessions\" WHERE \"Id\" = {sessionId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null) return null;
        var tracked = _context.DeliverySessions.Local.FirstOrDefault(s => s.Id == sessionId);
        if (tracked is null)
        {
            _context.Attach(snapshot);
            return snapshot;
        }
        _context.Entry(tracked).CurrentValues.SetValues(snapshot);
        _context.Entry(tracked).OriginalValues.SetValues(snapshot);
        return tracked;
    }

    public async Task<bool> LockOpenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Mismo bloqueo de fila que toman la recarga y el UPDATE de la recepcion.
        var session = await _context.DeliverySessions
            .FromSqlInterpolated($"SELECT * FROM \"DeliverySessions\" WHERE \"Id\" = {sessionId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return session?.Status == SessionStatus.Open;
    }

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
            .Include(s => s.Zones).ThenInclude(z => z.Zone)
            .Include(s => s.Restocks).ThenInclude(r => r.Items)
            .Include(s => s.InitialLoads)
            .Include(s => s.Deliveries)
            .Include(s => s.Promotions)
            .Include(s => s.ClosedPromotions)
            .Include(s => s.CashSettlement)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <summary>
    /// El stock a bordo se calcula sumando los movimientos, no se guarda. Asi no hay
    /// dos numeros para lo mismo, y una sesion cerrada con saldo distinto de cero es
    /// exactamente el faltante.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SessionStockLineDto>>> GetStockBalancesAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<SessionStockLineDto>>();
        var filas = await _context.SessionStockMovements
            .AsNoTracking()
            .Where(m => sessionIds.Contains(m.DeliverySessionId))
            .GroupBy(m => new { m.DeliverySessionId, m.ProductId, Detalle = m.Product.Detail })
            .Select(g => new
            {
                g.Key.DeliverySessionId,
                g.Key.ProductId,
                g.Key.Detalle,
                Full  = g.Where(m => m.State == ContainerState.Full).Sum(m => (int?)m.Quantity) ?? 0,
                Empty = g.Where(m => m.State == ContainerState.Empty).Sum(m => (int?)m.Quantity) ?? 0
            })
            .ToListAsync(cancellationToken);

        return filas.GroupBy(x => x.DeliverySessionId).ToDictionary(g => g.Key,
            g => (IReadOnlyList<SessionStockLineDto>)g
                .Select(x => new SessionStockLineDto(x.ProductId, x.Detalle, x.Full, x.Empty))
                .OrderByProduct(x => x.ProductDetail).ToList());
    }

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
            .OrderByProduct(x => x.ProductDetail)
            .ToList();
    }

    public async Task<IReadOnlyList<SessionDeliveryDto>> GetDeliveriesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => (await _context.Deliveries
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
                d.ContainerMovements.Where(m => m.Type != ContainerMovementType.LoanedToCustomer && m.Type != ContainerMovementType.LoanReturned && m.Type != ContainerMovementType.ExchangeCorrection).Select(m => new SessionDeliveryContainerDto(
                    m.ProductId, m.Product.Detail, m.Quantity)).ToList())
            {
                IsOutsideWorkingHours = d.IsOutsideWorkingHours,
                LoansOut = d.ContainerMovements.Where(m => m.Type == ContainerMovementType.LoanedToCustomer)
                    .Select(m => new SessionDeliveryContainerDto(m.ProductId, m.Product.Detail, m.Quantity)).ToList(),
                LoansIn = d.ContainerMovements.Where(m => m.Type == ContainerMovementType.LoanReturned)
                    .Select(m => new SessionDeliveryContainerDto(m.ProductId, m.Product.Detail, -m.Quantity)).ToList(),
                CustomerPhone = d.Customer.Phone,
                IsOutOfRoute = d.IsOutOfRoute,
                NoPurchaseReason = d.NoPurchaseReason
            })
            .ToListAsync(cancellationToken)).Select(d => d with
            {
                Items = d.Items.OrderByProduct(i => i.ProductDetail).ToList(),
                Containers = d.Containers.OrderByProduct(i => i.ProductDetail).ToList(),
                LoansOut = d.LoansOut.OrderByProduct(i => i.ProductDetail).ToList(),
                LoansIn = d.LoansIn.OrderByProduct(i => i.ProductDetail).ToList()
            }).ToList();

    public async Task<IReadOnlyList<SessionCustomerPaymentDto>> GetPaymentsByCustomerAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await _context.Payments
            .AsNoTracking()
            .Where(p => p.DeliverySessionId == sessionId)
            .GroupBy(p => new { p.CustomerId, p.Method })
            .Select(g => new SessionCustomerPaymentDto(g.Key.CustomerId, g.Key.Method, g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetCustomerBalancesAsync(
        Guid sessionId,
        DateTime untilUtc,
        CancellationToken cancellationToken = default)
    {
        var visitados = _context.Deliveries
            .Where(d => d.DeliverySessionId == sessionId)
            .Select(d => d.CustomerId);

        var vendido = await _context.Deliveries
            .AsNoTracking()
            .Where(d => visitados.Contains(d.CustomerId) && d.DeliveredAt <= untilUtc)
            .GroupBy(d => d.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(d => d.Total) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total, cancellationToken);

        var cobrado = await _context.Payments
            .AsNoTracking()
            .Where(p => visitados.Contains(p.CustomerId) && p.PaidAt <= untilUtc)
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total, cancellationToken);

        return vendido.ToDictionary(
            v => v.Key,
            v => v.Value - (cobrado.TryGetValue(v.Key, out var pagado) ? pagado : 0m));
    }

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
        CancellationToken cancellationToken = default,
        DateTime? closedFromUtc = null,
        DateTime? closedToUtc = null,
        bool includeOpen = false)
    {
        var query = _context.DeliverySessions
            .AsNoTracking()
            .Include(s => s.Driver)
            .Include(s => s.Vehicle)
            .Include(s => s.Zone)
            // Para marcar en el listado las salidas que todavia no rindieron.
            .Include(s => s.CashSettlement)
            .Include(s => s.Zones).ThenInclude(z => z.Zone)
            .AsQueryable();

        if (driverId is not null)
            query = query.Where(s => s.DriverId == driverId.Value);

        // Una salida que cruzó varias zonas aparece al filtrar por cualquiera de ellas.
        if (zoneId is not null)
            query = query.Where(s => s.Zones.Any(z => z.ZoneId == zoneId.Value));

        if (vehicleId is not null)
            query = query.Where(s => s.VehicleId == vehicleId.Value);

        // Se filtra por cuando SALIO. Una salida que cerro despues de medianoche
        // sigue siendo el reparto del dia en que arranco.
        if (openedFromUtc is not null)
            query = query.Where(s => s.OpenedAt >= openedFromUtc.Value);

        if (openedToUtc is not null)
            query = query.Where(s => s.OpenedAt < openedToUtc.Value);

        // Por cuando se RECIBIO: una salida de ayer recibida hoy es una recepcion
        // de hoy. Las abiertas no tienen cierre, por eso entran aparte.
        var porRecepcion = closedFromUtc is not null && closedToUtc is not null;
        if (porRecepcion)
            query = query.Where(s =>
                (s.ClosedAt >= closedFromUtc && s.ClosedAt < closedToUtc) ||
                (includeOpen && s.Status == SessionStatus.Open));

        var totalCount = await query.CountAsync(cancellationToken);

        // En el listado de recepciones van primero las que siguen en la calle, que
        // son las que esperan una accion, y despues las recibidas de la mas reciente.
        var ordenadas = porRecepcion
            ? query.OrderBy(s => s.ClosedAt != null)
                   .ThenByDescending(s => s.ClosedAt)
                   .ThenByDescending(s => s.OpenedAt)
            : query.OrderByDescending(s => s.OpenedAt);

        var items = await ordenadas
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Delivery>> GetCustomerVisitsAsync(Guid sessionId, Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.Deliveries.Where(d => d.DeliverySessionId == sessionId && d.CustomerId == customerId)
            .ToListAsync(cancellationToken);

    public async Task<Delivery?> GetDeliveryByClientRequestAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default)
        // Dos búsquedas y no un OR: con el OR Postgres no puede usar ningún índice y lee
        // todas las visitas, dos veces por cada visita que registra un chofer. La segunda
        // usa @> para que entre por el índice GIN; "= ANY" no lo aprovecha.
        => await _context.Deliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ClientRequestId == clientRequestId, cancellationToken)
        ?? await _context.Deliveries
            .FromSqlInterpolated($"SELECT * FROM \"Deliveries\" WHERE \"NoPurchaseRequestIds\" @> ARRAY[{clientRequestId}]::uuid[]")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
}
