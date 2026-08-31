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
}
