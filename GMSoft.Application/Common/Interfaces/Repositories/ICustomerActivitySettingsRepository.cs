using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ICustomerActivitySettingsRepository
{
    /// <summary>Sincrónico a propósito: lo usa la fábrica de la política al armar el pedido.</summary>
    CustomerActivitySettings? Get();
    Task<CustomerActivitySettings?> GetAsync(CancellationToken ct);
    Task SetAsync(int redAfterMissedWeeks, int blackAfterMissedWeeks, CancellationToken ct);
}
