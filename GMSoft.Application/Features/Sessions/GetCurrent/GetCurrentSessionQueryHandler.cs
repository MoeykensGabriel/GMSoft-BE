using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.GetCurrent;

public class GetCurrentSessionQueryHandler : IRequestHandler<GetCurrentSessionQuery, SessionDto?>
{
    private readonly ISessionRepository _sessions;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentSessionQueryHandler(
        ISessionRepository sessions,
        ICurrentUserService currentUser)
    {
        _sessions    = sessions;
        _currentUser = currentUser;
    }

    public async Task<SessionDto?> Handle(
        GetCurrentSessionQuery request,
        CancellationToken cancellationToken)
    {
        var driverId = _currentUser.DriverId
            ?? throw new ForbiddenException("Solo un chofer tiene sesion de reparto.");

        var abierta = await _sessions.GetOpenByDriverAsync(driverId, cancellationToken);
        if (abierta is null) return null;

        var session = await _sessions.GetWithDetailsAsync(abierta.Id, cancellationToken);
        if (session is null || session.Status != SessionStatus.Open) return null;

        var stock = await _sessions.GetStockBalanceAsync(session.Id, cancellationToken);

        return SessionMapping.ToDto(session, stock);
    }
}
