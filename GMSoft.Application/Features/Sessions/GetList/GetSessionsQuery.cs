using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetList;

public record GetSessionsQuery(
    int   Page     = 1,
    int   PageSize = 20,
    Guid? DriverId = null,
    Guid? ZoneId   = null) : IRequest<PagedResult<SessionDto>>;
