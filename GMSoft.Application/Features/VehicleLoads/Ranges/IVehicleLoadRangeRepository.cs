namespace GMSoft.Application.Features.VehicleLoads.Ranges;

public record VehicleLoadRangeDto(Guid ProductId, int? Min, int? Max);
public record VehicleLoadRangeInput(Guid ProductId, int? Min, int? Max);

public interface IVehicleLoadRangeRepository
{
    Task<IReadOnlyList<VehicleLoadRangeDto>> GetAsync(Guid vehicleId, CancellationToken ct);
    Task ReplaceAsync(Guid vehicleId, VehicleLoadRangeInput[] ranges, CancellationToken ct);
}
