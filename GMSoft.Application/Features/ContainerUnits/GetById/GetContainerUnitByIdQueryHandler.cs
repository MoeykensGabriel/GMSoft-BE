using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.ContainerUnits.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.GetById;

public class GetContainerUnitByIdQueryHandler
    : IRequestHandler<GetContainerUnitByIdQuery, ContainerUnitDto>
{
    private readonly IContainerUnitRepository _units;

    public GetContainerUnitByIdQueryHandler(IContainerUnitRepository units)
    {
        _units = units;
    }

    public async Task<ContainerUnitDto> Handle(
        GetContainerUnitByIdQuery request,
        CancellationToken cancellationToken)
    {
        var unit = await _units.GetWithDetailsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ContainerUnit), request.Id);

        return ContainerUnitMapping.ToDto(unit);
    }
}
