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

public record GetDriverRouteOrderQuery(Guid Id) : IRequest<RoutePlanningDto>;
public record SaveDriverRouteOrderCommand(Guid Id, Guid[] CustomerIds, string Version) : IRequest<RoutePlanningDto>;

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

    public async Task<RoutePlanningDto> Handle(GetDriverRouteOrderQuery request, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        EnsureAccess(session);
        var days = CustomerRouteAccess.Days(session);
        return RoutePlanningHandler.Snapshot(await routes.GetRouteAsync(session.VehicleId, session.ZoneId, days, ct),
            session.VehicleId, session.ZoneId, days);
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
            await routes.LockCustomersAsync(ct);
            var days = CustomerRouteAccess.Days(session);
            var current = await routes.GetRouteAsync(session.VehicleId, session.ZoneId, days, ct);
            if (request.CustomerIds.Length != current.Count ||
                request.CustomerIds.Distinct().Count() != current.Count ||
                request.CustomerIds.Except(current.Select(c => c.Id)).Any() ||
                RoutePlanningHandler.Snapshot(current, session.VehicleId, session.ZoneId, days).Version != request.Version)
                throw new ConflictException(ReloadMessage);
            var movedCount = RoutePlanningHandler.ApplyOrder(current, request.CustomerIds);
            if (movedCount > 0)
                await routes.AddChangeAsync(new RouteOrderChange
                {
                    VehicleId = session.VehicleId, ZoneId = session.ZoneId, DeliverySessionId = session.Id,
                    ChangedByUserId = user.UserId, ChangedAt = DateTime.UtcNow, MovedCount = movedCount
                }, ct);
            await unitOfWork.SaveChangesAsync(ct);
            result = RoutePlanningHandler.Snapshot(current, session.VehicleId, session.ZoneId, days);
        }, ct);
        return result;
    }
}
