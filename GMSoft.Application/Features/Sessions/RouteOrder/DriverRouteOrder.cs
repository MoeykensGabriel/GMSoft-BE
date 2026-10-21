using FluentValidation;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.RouteOrder;

/// <remarks>
/// El orden es propio de cada zona: con varias, se pide y se guarda una por vez
/// (<c>ZoneId</c>, entre las de la salida; sin ella, la principal).
/// </remarks>
public record GetDriverRouteOrderQuery(Guid Id, Guid? ZoneId = null) : IRequest<RoutePlanningDto>;
public record SaveDriverRouteOrderCommand(Guid Id, Guid[] CustomerIds, string Version, Guid? ZoneId = null) : IRequest<RoutePlanningDto>;

public class SaveDriverRouteOrderValidator : AbstractValidator<SaveDriverRouteOrderCommand>
{
    public SaveDriverRouteOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CustomerIds).NotNull();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public class DriverRouteOrderHandler(ISessionRepository sessions, IRoutePlanningRepository routes,
    ICurrentUserService user, IUnitOfWork unitOfWork)
    : IRequestHandler<GetDriverRouteOrderQuery, RoutePlanningDto>,
      IRequestHandler<SaveDriverRouteOrderCommand, RoutePlanningDto>
{
    public const string ReloadMessage = "El recorrido cambió mientras ordenabas. Volvé a cargarlo.";

    private void EnsureAccess(DeliverySession session)
    {
        if (user.DriverId != session.DriverId)
            throw new ForbiddenException("Esta salida es de otro chofer.");
        if (session.Status != SessionStatus.Open)
            throw new ConflictException("La salida ya se cerró.");
    }

    private async Task<Guid> ZoneAsync(DeliverySession session, Guid? requested, CancellationToken ct)
    {
        if (requested is not Guid zoneId) return session.ZoneId;
        if (!(await sessions.GetZoneIdsAsync(session.Id, ct)).Contains(zoneId))
            throw new BadRequestException("La zona elegida no es una de las de esta salida.");
        return zoneId;
    }

    public async Task<RoutePlanningDto> Handle(GetDriverRouteOrderQuery request, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        EnsureAccess(session);
        var zoneId = await ZoneAsync(session, request.ZoneId, ct);
        var days = CustomerRouteAccess.Days(session);
        return RoutePlanningHandler.Snapshot(await routes.GetRouteAsync(session.VehicleId, zoneId, days, ct),
            session.VehicleId, zoneId, days);
    }

    public async Task<RoutePlanningDto> Handle(SaveDriverRouteOrderCommand request, CancellationToken ct)
    {
        var validation = await new SaveDriverRouteOrderValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
        RoutePlanningDto result = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // El mismo orden de bloqueo que las visitas: primero la salida y después los clientes,
            // así no se ordena una salida que se está cerrando.
            var session = await sessions.LockAsync(request.Id, ct)
                ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
            EnsureAccess(session);
            var zoneId = await ZoneAsync(session, request.ZoneId, ct);
            await routes.LockCustomersAsync(ct);
            var days = CustomerRouteAccess.Days(session);
            var current = await routes.GetRouteAsync(session.VehicleId, zoneId, days, ct);
            if (request.CustomerIds.Length != current.Count ||
                request.CustomerIds.Distinct().Count() != current.Count ||
                request.CustomerIds.Except(current.Select(c => c.Id)).Any() ||
                RoutePlanningHandler.Snapshot(current, session.VehicleId, zoneId, days).Version != request.Version)
                throw new ConflictException(ReloadMessage);
            var movedCount = RoutePlanningHandler.ApplyOrder(current, request.CustomerIds);
            if (movedCount > 0)
                await routes.AddChangeAsync(new RouteOrderChange
                {
                    VehicleId = session.VehicleId, ZoneId = zoneId, DeliverySessionId = session.Id,
                    ChangedByUserId = user.UserId, ChangedAt = DateTime.UtcNow, MovedCount = movedCount
                }, ct);
            await unitOfWork.SaveChangesAsync(ct);
            result = RoutePlanningHandler.Snapshot(current, session.VehicleId, zoneId, days);
        }, ct);
        return result;
    }
}
