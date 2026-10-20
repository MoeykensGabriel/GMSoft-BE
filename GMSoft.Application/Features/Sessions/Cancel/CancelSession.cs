using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Cancel;

/// <summary>
/// Anular una salida abierta que no tuvo movimientos: por ejemplo, la oficina la abrió y
/// el chofer no vino. La carga vuelve a quedar pendiente en el camión para abrirla de
/// nuevo, y la salida queda oculta en listados e informes con quién, cuándo y por qué.
/// </summary>
public record CancelSessionCommand(Guid Id, string Reason) : IRequest;

public interface ISessionCancellationRepository
{
    /// <summary>Si la salida ya movió algo además de la carga inicial: visitas, cobros, recargas o promociones.</summary>
    Task<bool> HasActivityAsync(Guid sessionId, CancellationToken ct);

    /// <summary>El kilometraje con que volvió el camión en su última salida recibida, si tuvo.</summary>
    Task<int?> GetLastClosedKilometersAsync(Guid vehicleId, Guid excludeSessionId, CancellationToken ct);

    /// <summary>Devuelve la carga de la salida a pendiente, para que el camión pueda volver a salir.</summary>
    Task ReleaseLoadsAsync(Guid sessionId, CancellationToken ct);
}

public class CancelSessionCommandValidator : AbstractValidator<CancelSessionCommand>
{
    public CancelSessionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Escribí el motivo de la anulación.")
            .MaximumLength(300);
    }
}

public class CancelSessionCommandHandler(
    ISessionRepository sessions,
    ISessionCancellationRepository cancellation,
    IVehicleRepository vehicles,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    WorkingHoursService workingHours) : IRequestHandler<CancelSessionCommand>
{
    public async Task Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        // El endpoint ya es solo de admin; esto lo respalda del lado de la aplicacion.
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("La salida la anula la oficina.");

        var ahora = (await workingHours.EvaluateAsync(cancellationToken)).UtcNow;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // El mismo bloqueo que las visitas y la recepción: nada se registra en la salida mientras se anula.
            var session = await sessions.LockAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
            if (session.Status != SessionStatus.Open)
                throw new ConflictException("Solo se puede anular una salida que sigue abierta.");
            if (await cancellation.HasActivityAsync(session.Id, cancellationToken))
                throw new ConflictException(
                    "La salida ya tiene movimientos (visitas, cobros, recargas o promociones): no se puede anular. Hay que recibir el camión.");

            // Al abrir, el camión tomó el kilometraje de esta salida: vuelve al de su última vuelta.
            var vehicle = await vehicles.GetByIdAsync(session.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), session.VehicleId);
            var anterior = await cancellation.GetLastClosedKilometersAsync(session.VehicleId, session.Id, cancellationToken);
            if (anterior is int km && km < vehicle.CurrentKilometers && vehicle.CurrentKilometers == session.KilometersAtOpen)
            {
                vehicle.CurrentKilometers = km;
                vehicles.Update(vehicle);
            }

            await cancellation.ReleaseLoadsAsync(session.Id, cancellationToken);

            session.Status = SessionStatus.Cancelled;
            session.CancelledAt = ahora;
            session.CancelledByUserId = currentUser.UserId;
            session.CancellationReason = request.Reason.Trim();
            // Baja lógica: queda fuera de listados, resúmenes y estadísticas, pero no se borra. Se
            // marca a mano y no con Delete, para que EF no arrastre nada en cascada.
            session.IsDeleted = true;
            session.DeletedAt = ahora;
            sessions.Update(session);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
