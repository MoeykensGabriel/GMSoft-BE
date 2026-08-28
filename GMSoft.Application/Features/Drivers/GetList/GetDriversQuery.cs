using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetList;

public record GetDriversQuery(
    int     Page       = 1,
    int     PageSize   = 20,
    string? Search     = null,
    bool?   OnlyActive = null) : IRequest<PagedResult<DriverDto>>;
