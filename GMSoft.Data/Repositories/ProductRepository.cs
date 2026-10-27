using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => await _context.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => p.Id).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        bool? onlyPublished,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking();

        if (onlyPublished is not null)
            query = query.Where(p => p.IsPublished == onlyPublished.Value);

        // El total se cuenta antes de paginar, sobre el mismo filtro.
        var totalCount = await query.CountAsync(cancellationToken);

        // El catalogo es pequeno: ordenar antes de paginar permite ignorar acentos.
        var products = await query.ToListAsync(cancellationToken);
        var items = products
            .OrderByProduct(p => p.Detail)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DeliveryItems.AsNoTracking().AnyAsync(i => i.ProductId == id, cancellationToken)
        || await _context.SessionStockMovements.AsNoTracking().AnyAsync(m => m.ProductId == id, cancellationToken)
        || await _context.VehicleLoads.AsNoTracking()
               .AnyAsync(l => l.ProductId == id && l.ConsumedBySessionId == null, cancellationToken)
        || await HasContainerHistoryAsync(id, cancellationToken);

    public async Task<bool> HasContainerHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.ContainerMovements.AsNoTracking().AnyAsync(m => m.ProductId == id, cancellationToken)
        || await _context.ContainerUnits.AsNoTracking().AnyAsync(u => u.ProductId == id, cancellationToken)
        || await _context.CustomerContainerBalances.AsNoTracking()
               .AnyAsync(b => b.ProductId == id && b.Quantity != 0, cancellationToken);

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
