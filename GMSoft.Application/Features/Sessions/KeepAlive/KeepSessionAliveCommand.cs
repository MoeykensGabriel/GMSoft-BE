using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.KeepAlive;

public record SessionHeartbeatDto(SessionStatus Status, string? Token);
public record KeepSessionAliveCommand(Guid Id) : IRequest<SessionHeartbeatDto>;

/// <summary>Renueva un acceso válido mientras el chofer sigue con esa salida abierta.</summary>
public class KeepSessionAliveCommandHandler(
    ISessionRepository sessions, IDriverRepository drivers, ICurrentUserService currentUser, IJwtTokenService tokens)
    : IRequestHandler<KeepSessionAliveCommand, SessionHeartbeatDto>
{
    public async Task<SessionHeartbeatDto> Handle(KeepSessionAliveCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        if (!currentUser.IsInRole(AppRoles.Driver) || currentUser.DriverId != session.DriverId)
            throw new ForbiddenException("Solo el chofer de esta salida puede mantener su acceso.");
        if (session.Status == SessionStatus.Closed) return new(SessionStatus.Closed, null);

        var driver = await drivers.GetByIdAsync(session.DriverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), session.DriverId);
        if (!driver.IsActive || driver.ApplicationUserId != currentUser.UserId)
            throw new ForbiddenException("El chofer está desactivado o no pertenece a esta cuenta.");
        if (currentUser.UserId is not Guid userId || string.IsNullOrWhiteSpace(currentUser.UserName))
            throw new UnauthorizedException("El acceso debe validarse nuevamente.");

        // Conserva únicamente roles que el acceso actual ya tiene; no otorga permisos nuevos.
        var roles = new List<string> { AppRoles.Driver };
        if (currentUser.IsInRole(AppRoles.Admin)) roles.Add(AppRoles.Admin);
        var token = tokens.GenerateToken(new(userId, currentUser.UserName, currentUser.Email, roles, session.DriverId));
        return new(SessionStatus.Open, token);
    }
}
