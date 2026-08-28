using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetList;

public class GetSessionsQueryHandler : IRequestHandler<GetSessionsQuery, PagedResult<SessionDto>>
{
    private readonly ISessionRepository _sessions;

    public GetSessionsQueryHandler(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<PagedResult<SessionDto>> Handle(
        GetSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _sessions.GetPagedAsync(
            request.Page, request.PageSize, request.DriverId, request.ZoneId, cancellationToken);

        // El listado no trae el stock de cada sesion: serian N consultas para una
        // pantalla que muestra fechas y estados. El detalle si lo trae.
        var dtos = items
            .Select(s => SessionMapping.ToDto(s, []))
            .ToList();

        return new PagedResult<SessionDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
