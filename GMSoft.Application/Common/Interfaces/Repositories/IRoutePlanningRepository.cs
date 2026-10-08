using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IRoutePlanningRepository
{
    // Must be called inside the save transaction, before reading the list.
    Task LockCustomersAsync(CancellationToken ct);
    Task<IReadOnlyList<Customer>> GetRouteAsync(Guid vehicleId, Guid zoneId, int day, CancellationToken ct);
}
