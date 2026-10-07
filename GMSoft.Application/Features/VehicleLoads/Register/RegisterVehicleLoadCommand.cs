using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Register;

/// <summary>
/// La oficina sube mercadería al camión antes de que salga. Se manda toda la tanda
/// junta: si entrara a medias, el camión quedaría figurando con menos de lo que
/// realmente tiene arriba.
/// </summary>
/// <param name="ClientRequestId">
/// Identificador de la tanda generado por la pantalla. Repetirlo no vuelve a cargar:
/// devuelve la hora de la carga ya registrada.
/// </param>
public record RegisterVehicleLoadCommand(
    Guid VehicleId,
    IReadOnlyList<VehicleLoadItem> Items,
    int[]? RouteDays = null,
    Guid? ClientRequestId = null) : IRequest<RegisterVehicleLoadResult>;

public record VehicleLoadItem(Guid ProductId, int Quantity);

/// <summary>La hora definitiva de la carga, puesta por el servidor.</summary>
public record RegisterVehicleLoadResult(DateTime LoadedAt);
