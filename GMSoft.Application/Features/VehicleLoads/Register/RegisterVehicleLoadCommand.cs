using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Register;

/// <summary>
/// La oficina sube mercadería al camión antes de que salga. Se manda toda la tanda
/// junta: si entrara a medias, el camión quedaría figurando con menos de lo que
/// realmente tiene arriba.
/// </summary>
public record RegisterVehicleLoadCommand(
    Guid VehicleId,
    IReadOnlyList<VehicleLoadItem> Items) : IRequest;

public record VehicleLoadItem(Guid ProductId, int Quantity);
