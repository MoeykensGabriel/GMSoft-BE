using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class DriverRepository : Repository<Driver>, IDriverRepository
{
    public DriverRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Driver> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Drivers
            .AsNoTracking()
            .Include(d => d.Vehicle)
            .AsQueryable();

        if (onlyActive is not null)
            query = query.Where(d => d.IsActive == onlyActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d =>
                EF.Functions.ILike(d.FirstName, $"%{term}%") ||
                EF.Functions.ILike(d.LastName, $"%{term}%") ||
                EF.Functions.ILike(d.DocumentNumber, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Driver?> GetWithVehicleAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Drivers
            .Include(d => d.Vehicle)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .AsNoTracking()
            .AnyAsync(s => s.DriverId == id, cancellationToken);

    public async Task<bool> ExistsByDocumentAsync(
        string documentNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var term = documentNumber.Trim();

        return await _context.Drivers
            .AsNoTracking()
            .AnyAsync(d =>
                d.DocumentNumber == term &&
                (excludeId == null || d.Id != excludeId),
                cancellationToken);
    }
}
