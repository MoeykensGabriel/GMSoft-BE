using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleRepository : Repository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vehicles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                EF.Functions.ILike(v.Name, $"%{term}%") ||
                EF.Functions.ILike(v.LicensePlate, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByLicensePlateAsync(
        string licensePlate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v =>
                v.LicensePlate == licensePlate &&
                (excludeId == null || v.Id != excludeId),
                cancellationToken);

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .AsNoTracking()
            .AnyAsync(s => s.VehicleId == id, cancellationToken);
}
