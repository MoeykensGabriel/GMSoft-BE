using GMSoft.Application.Features.ContainerUnits.Common;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.GetById;

public record GetContainerUnitByIdQuery(Guid Id) : IRequest<ContainerUnitDto>;
