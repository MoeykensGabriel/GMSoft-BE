using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyPublished,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking();

        if (onlyPublished is not null)
            query = query.Where(p => p.IsPublished == onlyPublished.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Detail, $"%{term}%") ||
                (p.CommercialDetail != null && EF.Functions.ILike(p.CommercialDetail, $"%{term}%")));
        }

        // El total se cuenta antes de paginar, sobre el mismo filtro.
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Detail)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DeliveryItems.AsNoTracking().AnyAsync(i => i.ProductId == id, cancellationToken)
        || await _context.SessionStockMovements.AsNoTracking().AnyAsync(m => m.ProductId == id, cancellationToken)
        || await _context.ContainerMovements.AsNoTracking().AnyAsync(m => m.ProductId == id, cancellationToken);

    public async Task<bool> ExistsByDetailAsync(
        string detail,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var term = detail.Trim();

        return await _context.Products
            .AsNoTracking()
            .AnyAsync(p =>
                p.Detail.ToLower() == term.ToLower() &&
                (excludeId == null || p.Id != excludeId),
                cancellationToken);
    }
}
