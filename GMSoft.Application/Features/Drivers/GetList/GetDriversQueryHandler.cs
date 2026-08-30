using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetList;

public class GetDriversQueryHandler : IRequestHandler<GetDriversQuery, PagedResult<DriverDto>>
{
    private readonly IDriverRepository _drivers;
    private readonly IIdentityService _identityService;

    public GetDriversQueryHandler(IDriverRepository drivers, IIdentityService identityService)
    {
        _drivers         = drivers;
        _identityService = identityService;
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

        // Una sola consulta para los usuarios de toda la pagina, no una por chofer.
        var usuarios = await _identityService.GetUserNamesAsync(
            items.Where(d => d.ApplicationUserId is not null)
                 .Select(d => d.ApplicationUserId!.Value)
                 .ToList(),
            cancellationToken);

        return new PagedResult<DriverDto>(
            items.Select(d => DriverMapping.ToDto(
                d,
                d.ApplicationUserId is not null && usuarios.TryGetValue(d.ApplicationUserId.Value, out var u)
                    ? u
                    : null)).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
