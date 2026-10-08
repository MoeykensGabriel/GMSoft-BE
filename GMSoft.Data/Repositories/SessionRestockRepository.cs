using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class SessionRestockRepository(AppDbContext context)
    : Repository<SessionRestock>(context), ISessionRestockRepository
{
    public async Task<bool> IsSessionOpenForUpdateAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // El UPDATE del cierre usa el mismo bloqueo de fila. Si gano el cierre,
        // vemos Closed; si gano la recarga, el cierre espera y cuenta sus movimientos.
        var session = await _context.DeliverySessions
            .FromSqlInterpolated($"SELECT * FROM \"DeliverySessions\" WHERE \"Id\" = {sessionId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return session?.Status == GMSoft.Domain.Enums.SessionStatus.Open;
    }

    public async Task<SessionRestock?> GetByClientRequestAsync(
        Guid clientRequestId, CancellationToken cancellationToken = default)
        => await _context.SessionRestocks.AsNoTracking().Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.ClientRequestId == clientRequestId, cancellationToken);
}
