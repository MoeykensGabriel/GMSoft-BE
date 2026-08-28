using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ContainerBalanceRepository
    : Repository<CustomerContainerBalance>, IContainerBalanceRepository
{
    public ContainerBalanceRepository(AppDbContext context) : base(context) { }

    public async Task<CustomerContainerBalance?> GetAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerContainerBalances
            .FirstOrDefaultAsync(
                b => b.CustomerId == customerId && b.ProductId == productId,
                cancellationToken);

    public async Task<IReadOnlyList<CustomerContainerBalance>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerContainerBalances
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.CustomerId == customerId)
            .OrderBy(b => b.Product.Detail)
            .ToListAsync(cancellationToken);
}
