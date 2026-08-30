using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetDeliveries;

public class GetSessionDeliveriesQueryHandler
    : IRequestHandler<GetSessionDeliveriesQuery, IReadOnlyList<SessionDeliveryDto>>
{
    private readonly ISessionRepository _sessions;
    private readonly ICurrentUserService _currentUser;

    public GetSessionDeliveriesQueryHandler(
        ISessionRepository sessions,
        ICurrentUserService currentUser)
    {
        _sessions    = sessions;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SessionDeliveryDto>> Handle(
        GetSessionDeliveriesQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        // Mismo criterio que el detalle de la sesion: el chofer ve las suyas, el
        // admin ve todas. Sin esto, cualquier chofer leeria el recorrido de otro.
        if (!_currentUser.IsInRole(AppRoles.Admin) && _currentUser.DriverId != session.DriverId)
            throw new ForbiddenException("Esta salida es de otro chofer.");

        return await _sessions.GetDeliveriesAsync(session.Id, cancellationToken);
    }
}
