using GMSoft.Application.Features.VehicleLoads.Common;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetPending;

/// <summary>
/// Lo que el camión tiene arriba esperando salir. Lo lee la oficina para saber qué
/// le falta cargar, y el chofer al abrir la salida para confirmar que es lo que ve
/// en el camión.
/// </summary>
public record GetPendingVehicleLoadQuery(Guid VehicleId)
    : IRequest<IReadOnlyList<VehicleLoadLineDto>>;
