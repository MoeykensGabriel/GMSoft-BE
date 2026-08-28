using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Assign;

/// <summary>Entrega la unidad a un cliente. Queda en su poder hasta que se recupere.</summary>
public record AssignContainerUnitCommand(
    Guid    Id,
    Guid    CustomerId,
    string? Notes) : IRequest;
