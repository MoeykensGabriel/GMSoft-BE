using GMSoft.Application.Features.Customers.Removal;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class CustomerRemovalRepository(AppDbContext db) : ICustomerRemovalRepository
{
    public async Task LockAsync(CancellationToken ct)
    {
        // Serialize requests/resolutions, including reuse of a key for another customer.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(180018)", ct);
        db.ChangeTracker.Clear();
    }
    private IQueryable<CustomerRemovalRequest> Full() => db.Set<CustomerRemovalRequest>()
        .Include(r => r.Customer).Include(r => r.Driver).Include(r => r.Vehicle).Include(r => r.Zone);
    public Task<CustomerRemovalRequest?> FindAsync(Guid id, bool byClientRequest, CancellationToken ct)
        => Full().FirstOrDefaultAsync(r => byClientRequest ? r.ClientRequestId == id : r.Id == id, ct);
    public Task<bool> HasPendingAsync(Guid customerId, CancellationToken ct)
        => db.Set<CustomerRemovalRequest>().AnyAsync(r => r.CustomerId == customerId && r.Status == RemovalStatus.Pending, ct);
    public async Task<List<RemovalContainerSnapshot>> ContainersAsync(Guid customerId, CancellationToken ct)
    {
        var balances = await db.CustomerContainerBalances.AsNoTracking().Where(b => b.CustomerId == customerId && b.Quantity > 0)
            .Select(b => new RemovalContainerSnapshot { ProductId = b.ProductId, ProductDetail = b.Product.Detail, Quantity = b.Quantity }).ToListAsync(ct);
        var units = await db.ContainerUnits.AsNoTracking().Where(u => u.CurrentCustomerId == customerId)
            .GroupBy(u => new { u.ProductId, u.Product.Detail })
            .Select(g => new RemovalContainerSnapshot { ProductId = g.Key.ProductId, ProductDetail = g.Key.Detail, Quantity = g.Count() }).ToListAsync(ct);
        return balances.Concat(units).GroupBy(c => c.ProductId).OrderBy(g => g.Key)
            .Select(g => new RemovalContainerSnapshot { ProductId = g.Key, ProductDetail = g.First().ProductDetail, Quantity = g.Sum(c => c.Quantity) }).ToList();
    }
    public async Task AddAsync(CustomerRemovalRequest request, CancellationToken ct) => await db.AddAsync(request, ct);
    public async Task<RemovalPage> ListAsync(ListRemovals query, CancellationToken ct)
    {
        var pending = await db.Set<CustomerRemovalRequest>().CountAsync(r => r.Status == RemovalStatus.Pending, ct);
        var filtered = Full().AsNoTracking().Where(r => query.Status == null || r.Status == query.Status);
        var total = await filtered.CountAsync(ct);
        var items = await filtered.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(RemovalDto.From).ToList(), total, pending, query.Page, query.PageSize);
    }
}
