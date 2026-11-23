using FluentValidation;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Access;

/// <summary>La app avisa que cierra la sesión del chofer y por qué.</summary>
public record LogoutCommand(AccessEndReason Reason) : IRequest;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
        // Los vencimientos y los cambios de cuenta los anota el servidor, no la app.
        => RuleFor(x => x.Reason).Must(r => r is AccessEndReason.Manual or AccessEndReason.ReceptionCompleted
            or AccessEndReason.DepartureCancelled).WithMessage("Motivo de salida inválido.");
}

public class LogoutCommandHandler(IAccessEventRepository events, IUnitOfWork unitOfWork, ICurrentUserService user)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Solo interesa el acceso de los choferes; la oficina no se controla.
        if (user.UserId is not Guid userId || user.DriverId is not Guid driverId) return;
        await events.AddAsync(new AccessEvent
        {
            UserId = userId, DriverId = driverId, OccurredAt = DateTime.UtcNow, Kind = AccessEventKind.Logout,
            Reason = request.Reason, Device = AccessDevice.Describe(user.UserAgent),
            Client = AccessDevice.Client(user.UserAgent, null),
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// El servidor rechazó el token de un chofer. Lo manda la validación del JWT, antes de
/// que exista un usuario autenticado. Un mismo token se anota una sola vez: la app hace
/// varios pedidos a la vez y todos rebotan.
/// </summary>
public record RecordAccessRejectionCommand(Guid UserId, Guid DriverId, string? TokenId, AccessEndReason Reason,
    string? UserAgent) : IRequest;

public class RecordAccessRejectionCommandHandler(IAccessEventRepository events, IUnitOfWork unitOfWork)
    : IRequestHandler<RecordAccessRejectionCommand>
{
    public async Task Handle(RecordAccessRejectionCommand request, CancellationToken cancellationToken)
    {
        if (request.TokenId is { } tokenId && await events.HasRejectionAsync(request.UserId, tokenId, cancellationToken))
            return;
        await events.AddAsync(new AccessEvent
        {
            UserId = request.UserId, DriverId = request.DriverId, OccurredAt = DateTime.UtcNow,
            Kind = AccessEventKind.Rejected, Reason = request.Reason, TokenId = request.TokenId,
            Device = AccessDevice.Describe(request.UserAgent), Client = AccessDevice.Client(request.UserAgent, null),
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
