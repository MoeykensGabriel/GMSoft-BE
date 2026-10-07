using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetPendingSummary;

/// <summary>
/// Lo que el camión tiene arriba esperando salir, sumado por producto. Es lo que ve
/// el chofer: a él le importa cuánto lleva, no en cuántas tandas se cargó.
/// </summary>
public record GetPendingVehicleLoadSummaryQuery(Guid VehicleId) : IRequest<VehicleLoadSummaryDto>;

/// <param name="RouteDays">Días ISO que cubrirá la próxima salida, sin repetir y en orden.</param>
public record VehicleLoadSummaryDto(
    int[] RouteDays,
    IReadOnlyList<VehicleLoadSummaryLineDto> Lines);

public record VehicleLoadSummaryLineDto(
    Guid   ProductId,
    string ProductDetail,
    int    Quantity);
