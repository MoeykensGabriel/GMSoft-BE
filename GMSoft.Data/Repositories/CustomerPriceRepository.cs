using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class CustomerPriceRepository : Repository<CustomerProductPrice>, ICustomerPriceRepository
{
    public CustomerPriceRepository(AppDbContext context) : base(context) { }

    public async Task<decimal?> GetPriceAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerProductPrices
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.ProductId == productId)
            .Select(p => (decimal?)p.Price)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerProductPrice>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerProductPrices
            .AsNoTracking()
            .Include(p => p.Product)
            .Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.Product.Detail)
            .ToListAsync(cancellationToken);
}
