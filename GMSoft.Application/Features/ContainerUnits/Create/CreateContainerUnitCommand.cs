using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Create;

/// <summary>Alta de una unidad. Nace en el deposito, sin cliente.</summary>
public record CreateContainerUnitCommand(Guid ProductId, string SerialNumber) : IRequest<Guid>;
