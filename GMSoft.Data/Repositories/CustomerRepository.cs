using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        Guid? zoneId,
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Customers
            .AsNoTracking()
            .Include(c => c.Zone)
            .AsQueryable();

        if (zoneId is not null)
            query = query.Where(c => c.ZoneId == zoneId.Value);

        if (onlyActive is not null)
            query = query.Where(c => c.IsActive == onlyActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.ContactName, $"%{term}%") ||
                (c.BusinessName != null && EF.Functions.ILike(c.BusinessName, $"%{term}%")) ||
                EF.Functions.ILike(c.Address, $"%{term}%") ||
                EF.Functions.ILike(c.Phone, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Filtrado por zona es la hoja de ruta y va en orden de recorrido. Sin zona,
        // ese orden no significa nada entre clientes de zonas distintas, asi que se
        // ordena por nombre.
        query = zoneId is not null
            ? query.OrderBy(c => c.RouteOrder)
            : query.OrderBy(c => c.BusinessName ?? c.ContactName);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Customer?> GetWithZoneAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Customers
            .Include(c => c.Zone)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<int> GetNextRouteOrderAsync(
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        // Max sobre int? para que una zona vacia devuelva null en vez de romper.
        var ultimo = await _context.Customers
            .AsNoTracking()
            .Where(c => c.ZoneId == zoneId)
            .MaxAsync(c => (int?)c.RouteOrder, cancellationToken);

        return (ultimo ?? 0) + 1;
    }

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Deliveries.AsNoTracking().AnyAsync(d => d.CustomerId == id, cancellationToken)
        || await _context.ContainerMovements.AsNoTracking().AnyAsync(m => m.CustomerId == id, cancellationToken)
        || await _context.Payments.AsNoTracking().AnyAsync(p => p.CustomerId == id, cancellationToken);
}
