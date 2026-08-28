using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Update;

/// <summary>
/// Correccion del numero de serie. El estado y el cliente NO se tocan por aca: se
/// mueven con asignar, recuperar y dar de baja, que dejan asiento en el libro mayor.
/// </summary>
public record UpdateContainerUnitCommand(Guid Id, string SerialNumber) : IRequest;
