using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetList;

public class GetDriversQueryHandler : IRequestHandler<GetDriversQuery, PagedResult<DriverDto>>
{
    private readonly IDriverRepository _drivers;

    public GetDriversQueryHandler(IDriverRepository drivers)
    {
        _drivers = drivers;
    }

    public async Task<PagedResult<DriverDto>> Handle(
        GetDriversQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _drivers.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.OnlyActive,
            cancellationToken);

        return new PagedResult<DriverDto>(
            items.Select(d => DriverMapping.ToDto(d)).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
