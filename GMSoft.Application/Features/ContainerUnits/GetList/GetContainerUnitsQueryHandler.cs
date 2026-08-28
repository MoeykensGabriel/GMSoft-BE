using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.ContainerUnits.Common;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.GetList;

public class GetContainerUnitsQueryHandler
    : IRequestHandler<GetContainerUnitsQuery, PagedResult<ContainerUnitDto>>
{
    private readonly IContainerUnitRepository _units;

    public GetContainerUnitsQueryHandler(IContainerUnitRepository units)
    {
        _units = units;
    }

    public async Task<PagedResult<ContainerUnitDto>> Handle(
        GetContainerUnitsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _units.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.ProductId,
            request.Status,
            request.CustomerId,
            cancellationToken);

        return new PagedResult<ContainerUnitDto>(
            items.Select(ContainerUnitMapping.ToDto).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
