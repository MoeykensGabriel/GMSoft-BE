using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Validation;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.UpdateDays;

public record UpdateVehicleRouteDaysCommand(Guid VehicleId, int[] RouteDays) : IRequest;

public class UpdateVehicleRouteDaysCommandValidator : AbstractValidator<UpdateVehicleRouteDaysCommand>
{
    public UpdateVehicleRouteDaysCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.RouteDays).ValidVisitDays();
    }
}

public class UpdateVehicleRouteDaysCommandHandler(
    IVehicleLoadRepository loads, IVehicleRepository vehicles, ISessionRepository sessions,
    ICurrentUserService user, IUnitOfWork work) : IRequestHandler<UpdateVehicleRouteDaysCommand>
{
    public async Task Handle(UpdateVehicleRouteDaysCommand request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo el administrador puede cambiar los días del recorrido.");
        _ = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);
        if (await sessions.HasOpenSessionForVehicleAsync(request.VehicleId, null, cancellationToken))
            throw new ConflictException("El vehículo ya tiene una salida abierta. No se pueden cambiar sus días.");
        var pending = await loads.GetPendingAsync(request.VehicleId, cancellationToken);
        if (pending.Count == 0)
            throw new ConflictException("Primero registrá la carga inicial del vehículo.");
        foreach (var load in pending)
        {
            load.RouteDays = request.RouteDays.Order().ToArray();
            loads.Update(load);
        }
        await work.SaveChangesAsync(cancellationToken);
    }
}
