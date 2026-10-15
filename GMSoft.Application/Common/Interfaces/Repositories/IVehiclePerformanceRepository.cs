using GMSoft.Application.Features.Reports.VehiclePerformance;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IVehiclePerformanceRepository
{
    Task<IReadOnlyList<VehiclePerformanceLineDto>> GetAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}
