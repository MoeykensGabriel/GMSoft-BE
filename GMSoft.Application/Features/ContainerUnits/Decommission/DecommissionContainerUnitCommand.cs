using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Decommission;

/// <summary>
/// Baja definitiva: rota, perdida o inservible. El motivo es obligatorio, porque es
/// plata que sale del patrimonio y tiene que quedar explicado por que.
/// </summary>
public record DecommissionContainerUnitCommand(Guid Id, string Reason) : IRequest;
