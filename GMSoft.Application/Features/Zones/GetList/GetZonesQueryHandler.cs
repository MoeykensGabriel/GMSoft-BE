using Mapster;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Zones.Common;
using MediatR;

namespace GMSoft.Application.Features.Zones.GetList;

public class GetZonesQueryHandler : IRequestHandler<GetZonesQuery, PagedResult<ZoneDto>>
{
    private readonly IZoneRepository _zones;

    public GetZonesQueryHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<PagedResult<ZoneDto>> Handle(
        GetZonesQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _zones.GetPagedAsync(
            request.Page, request.PageSize, request.Search, request.OnlyActive, cancellationToken);

        return new PagedResult<ZoneDto>(
            items.Adapt<List<ZoneDto>>(), totalCount, request.Page, request.PageSize);
    }
}
