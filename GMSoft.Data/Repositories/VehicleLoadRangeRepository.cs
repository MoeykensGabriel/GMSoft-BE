using GMSoft.Application.Common;
using GMSoft.Application.Features.VehicleLoads.Ranges;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleLoadRangeRepository(AppDbContext db) : IVehicleLoadRangeRepository
{
    public async Task<IReadOnlyList<VehicleLoadRangeDto>> GetAsync(Guid vehicleId, CancellationToken ct)
    {
        var rows = await (from range in db.VehicleLoadRanges.AsNoTracking()
                          join product in db.Products.IgnoreQueryFilters() on range.ProductId equals product.Id
                          where range.VehicleId == vehicleId
                          select new { range.ProductId, range.Min, range.Max, product.Detail }).ToListAsync(ct);
        return rows.OrderByProduct(r => r.Detail)
            .Select(r => new VehicleLoadRangeDto(r.ProductId, r.Min, r.Max)).ToList();
    }

    public async Task ReplaceAsync(Guid vehicleId, VehicleLoadRangeInput[] ranges, CancellationToken ct)
    {
        // El handler mantiene el bloqueo del vehículo y la transacción durante todo el reemplazo.
        await db.VehicleLoadRanges.Where(r => r.VehicleId == vehicleId).ExecuteDeleteAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<VehicleLoadRange>()
                     .Where(e => e.Entity.VehicleId == vehicleId).ToArray())
            entry.State = EntityState.Detached;
        db.VehicleLoadRanges.AddRange(ranges.Select(r => new VehicleLoadRange
        {
            VehicleId = vehicleId, ProductId = r.ProductId, Min = r.Min, Max = r.Max
        }));
    }
}
