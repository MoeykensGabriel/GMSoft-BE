using GMSoft.Application.Features.Reports.VehiclePerformance;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IVehiclePerformanceRepository
{
    Task<IReadOnlyList<VehiclePerformanceLineDto>> GetAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Lo vendido por cada chofer en las salidas abiertas en el rango, para su comisión.</summary>
    Task<IReadOnlyList<DriverSalesDto>> GetDriverSalesAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}
