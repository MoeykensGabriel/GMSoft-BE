using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ISessionRepository : IRepository<DeliverySession>
{
    /// <summary>La sesion abierta de un chofer, si tiene. Un chofer no puede tener dos.</summary>
    Task<DeliverySession?> GetOpenByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>Si el vehiculo ya esta en la calle con otra sesion abierta.</summary>
    Task<bool> HasOpenSessionForVehicleAsync(
        Guid vehicleId,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Trae la sesion con chofer, vehiculo y zona para armar el DTO.</summary>
    Task<DeliverySession?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stock a bordo, sumando los movimientos por producto y estado. Es la unica
    /// fuente: no hay un campo de stock que pueda quedar desincronizado.
    /// </summary>
    Task<IReadOnlyList<SessionStockLineDto>> GetStockBalanceAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DeliverySession> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? driverId,
        Guid? zoneId,
        CancellationToken cancellationToken = default);
}
