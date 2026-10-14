using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IWorkingHoursRepository
{
    Task<WorkingHoursSettings?> GetAsync(CancellationToken ct);
    Task SetAsync(WorkingDay[] days, CancellationToken ct);
}
