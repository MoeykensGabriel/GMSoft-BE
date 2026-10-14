using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleLoadRepository : Repository<VehicleLoad>, IVehicleLoadRepository
{
    public VehicleLoadRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<VehicleLoad>> GetPendingAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
        // Sin AsNoTracking: quien abre la sesion las marca como consumidas sobre
        // estas mismas instancias.
        => await _context.VehicleLoads
            .Include(l => l.Product)
            .Where(l => l.VehicleId == vehicleId && l.ConsumedBySessionId == null)
            .OrderBy(l => l.LoadedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VehicleLoad>> GetRemovedAsync(
        Guid vehicleId,
        int take,
        CancellationToken cancellationToken = default)
        => await _context.VehicleLoads
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(l => l.Product)
            .Where(l => l.VehicleId == vehicleId && l.IsDeleted)
            .OrderByDescending(l => l.DeletedAt)
            .ThenBy(l => l.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<DateTime?> GetLoadedAtByClientRequestAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default)
        => await _context.VehicleLoads
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(l => l.ClientRequestId == clientRequestId)
            .Select(l => (DateTime?)l.LoadedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
