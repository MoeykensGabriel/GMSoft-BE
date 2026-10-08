using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IPromotionRepository : IRepository<Promotion>
{
    // Locks de transaccion: reintentos y cierres simultaneos se leen despues de esperar.
    Task LockAsync(string key, CancellationToken ct);
    Task<Promotion?> FindRequestAsync(Guid requestId, bool closing, CancellationToken ct);
    Task<Promotion?> GetDetailsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Promotion>> GetPendingAsync(Guid vehicleId, CancellationToken ct);
    Task<(IReadOnlyList<Promotion> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? status, Guid? vehicleId, DateTime? fromUtc, DateTime? toUtc,
        DateOnly today, CancellationToken ct);
}

public interface IPromotionSettingsRepository
{
    Task<int> GetPickupDaysAsync(CancellationToken ct);
    Task SetPickupDaysAsync(int days, CancellationToken ct);
}

