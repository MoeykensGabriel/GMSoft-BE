using GMSoft.Application.Features.Reports.VehicleKilometers;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IVehicleKilometersRepository
{
    /// <summary>Salidas cerradas con kilometraje de vuelta, abiertas en el rango.</summary>
    Task<IReadOnlyList<SessionKilometersRow>> GetClosedSessionsAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}
