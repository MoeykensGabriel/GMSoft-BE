using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetById;

public class GetSessionByIdQueryHandler : IRequestHandler<GetSessionByIdQuery, SessionDto>
{
    private readonly ISessionRepository _sessions;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoutePlanningRepository _routes;
    private readonly IIdentityService _identity;

    public GetSessionByIdQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser,
        IRoutePlanningRepository routes, IIdentityService identity)
    {
        _sessions    = sessions;
        _currentUser = currentUser;
        _routes = routes;
        _identity = identity;
    }

    public async Task<SessionDto> Handle(GetSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetWithDetailsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        // El chofer ve las suyas y nada mas. El admin ve todas.
        if (!_currentUser.IsInRole(AppRoles.Admin) && _currentUser.DriverId != session.DriverId)
            throw new ForbiddenException("Esta sesion es de otro chofer.");

        var stock = await _sessions.GetStockBalanceAsync(session.Id, cancellationToken);

        return SessionMapping.ToDto(session, stock) with
        {
            DriverRouteOrderChange = await Customers.RoutePlanning.RoutePlanningHandler.ReadLastChange(
                _routes, _identity, session.VehicleId, session.ZoneId, cancellationToken, session.Id)
        };
    }
}
