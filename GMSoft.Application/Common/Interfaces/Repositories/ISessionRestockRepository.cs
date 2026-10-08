using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ISessionRestockRepository : IRepository<SessionRestock>
{
    Task<SessionRestock?> GetByClientRequestAsync(Guid clientRequestId, CancellationToken cancellationToken = default);

    /// <summary>Dentro de la transaccion: bloquea la salida y vuelve a leer su estado.</summary>
    Task<bool> IsSessionOpenForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
