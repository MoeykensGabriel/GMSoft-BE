using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ContainerUnitRepository : Repository<ContainerUnit>, IContainerUnitRepository
{
    public ContainerUnitRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<ContainerUnit> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        Guid? productId,
        ContainerUnitStatus? status,
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ContainerUnits
            .AsNoTracking()
            .Include(u => u.Product)
            .Include(u => u.CurrentCustomer)
            .AsQueryable();

        if (productId is not null)
            query = query.Where(u => u.ProductId == productId.Value);

        if (status is not null)
            query = query.Where(u => u.Status == status.Value);

        if (customerId is not null)
            query = query.Where(u => u.CurrentCustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => EF.Functions.ILike(u.SerialNumber, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.SerialNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ContainerUnit?> GetWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.ContainerUnits
            .Include(u => u.Product)
            .Include(u => u.CurrentCustomer)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<bool> ExistsBySerialNumberAsync(
        string serialNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var term = serialNumber.Trim();

        return await _context.ContainerUnits
            .AsNoTracking()
            .AnyAsync(u =>
                u.SerialNumber.ToLower() == term.ToLower() &&
                (excludeId == null || u.Id != excludeId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<ContainerUnit>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.ContainerUnits
            .AsNoTracking()
            .Include(u => u.Product)
            .Where(u => u.CurrentCustomerId == customerId && u.Status == ContainerUnitStatus.WithCustomer)
            .OrderBy(u => u.SerialNumber)
            .ToListAsync(cancellationToken);
}
