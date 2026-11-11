using GMSoft.Application.Features.Reports.DriverControl;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IDriverControlRepository
{
    /// <summary>Salidas abiertas en el rango (sin las anuladas) y sus visitas.</summary>
    Task<(IReadOnlyList<DriverControlSessionRow> Sessions, IReadOnlyList<DriverControlVisitRow> Visits)> GetAsync(
        DateTime fromUtc, DateTime toUtc, Guid? driverId, CancellationToken cancellationToken = default);
}
