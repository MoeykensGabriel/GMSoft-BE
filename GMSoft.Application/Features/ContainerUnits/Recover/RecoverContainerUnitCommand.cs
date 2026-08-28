using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Recover;

/// <summary>El cliente devuelve la unidad y vuelve al deposito.</summary>
public record RecoverContainerUnitCommand(Guid Id, string? Notes) : IRequest;
