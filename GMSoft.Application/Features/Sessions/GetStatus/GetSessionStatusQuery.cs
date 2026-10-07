using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetStatus;

public record SessionStatusDto(Guid Id, Guid VehicleId, SessionStatus Status, DateTime? ClosedAt);
public record GetSessionStatusQuery(Guid Id) : IRequest<SessionStatusDto>;

/// <summary>Estado de una salida concreta, incluso después de la recepción.</summary>
public class GetSessionStatusQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser)
    : IRequestHandler<GetSessionStatusQuery, SessionStatusDto>
{
    public async Task<SessionStatusDto> Handle(GetSessionStatusQuery request, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        if (!currentUser.IsInRole(AppRoles.Admin) && currentUser.DriverId != session.DriverId)
            throw new ForbiddenException("Esta salida es de otro chofer.");
        return new(session.Id, session.VehicleId, session.Status, session.ClosedAt);
    }
}
