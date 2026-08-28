using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ZoneRepository : Repository<Zone>, IZoneRepository
{
    public ZoneRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Zone> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Zones.AsNoTracking();

        if (onlyActive is not null)
            query = query.Where(z => z.IsActive == onlyActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(z => EF.Functions.ILike(z.Name, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(z => z.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var term = name.Trim();

        return await _context.Zones
            .AsNoTracking()
            .AnyAsync(z =>
                z.Name.ToLower() == term.ToLower() &&
                (excludeId == null || z.Id != excludeId),
                cancellationToken);
    }

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Customers.AsNoTracking().AnyAsync(c => c.ZoneId == id, cancellationToken)
        || await _context.DeliverySessions.AsNoTracking().AnyAsync(s => s.ZoneId == id, cancellationToken);
}
