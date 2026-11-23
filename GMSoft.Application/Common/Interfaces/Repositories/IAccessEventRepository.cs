using GMSoft.Application.Features.Access;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IAccessEventRepository
{
    Task AddAsync(AccessEvent accessEvent, CancellationToken ct = default);

    /// <summary>Si ese token ya quedó anotado como rechazado.</summary>
    Task<bool> HasRejectionAsync(Guid userId, string tokenId, CancellationToken ct = default);

    /// <summary>
    /// Los eventos del rango y, por chofer, el último anterior al rango: hace falta para
    /// saber si la primera entrada del día vino después de una salida registrada.
    /// </summary>
    Task<IReadOnlyList<AccessEventRow>> GetAsync(DateTime fromUtc, DateTime toUtc, Guid? driverId, CancellationToken ct = default);
}
