using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Application.Features.VehicleLoads.Common;
using MediatR;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.VehicleLoads.Register;

public class RegisterVehicleLoadCommandHandler
    : IRequestHandler<RegisterVehicleLoadCommand, RegisterVehicleLoadResult>
{
    private readonly IVehicleLoadRepository _loads;
    private readonly IVehicleRepository _vehicles;
    private readonly ISessionRepository _sessions;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkingHoursService _workingHours;

    public RegisterVehicleLoadCommandHandler(
        IVehicleLoadRepository loads,
        IVehicleRepository vehicles,
        ISessionRepository sessions,
        IRepository<Product> products,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        WorkingHoursService workingHours)
    {
        _loads       = loads;
        _vehicles    = vehicles;
        _sessions    = sessions;
        _products    = products;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
        _workingHours = workingHours;
    }

    public async Task<RegisterVehicleLoadResult> Handle(
        RegisterVehicleLoadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo el administrador puede preparar la carga del camion.");

        // Reintento de una tanda que ya entro: la respuesta anterior se perdio en el
        // camino. Sumarla de nuevo dejaria al camion con el doble de lo que subio.
        if (request.ClientRequestId is Guid tanda &&
            await _loads.GetLoadedAtByClientRequestAsync(tanda, cancellationToken) is DateTime yaCargada)
            return new RegisterVehicleLoadResult(yaCargada);

        RegisterVehicleLoadResult result = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var vehicle = await _vehicles.LockAsync(request.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);

            if (request.ClientRequestId is Guid retryKey &&
                await _loads.GetLoadedAtByClientRequestAsync(retryKey, cancellationToken) is DateTime loadedAt)
            {
                result = new RegisterVehicleLoadResult(loadedAt);
                return;
            }

            // Un camion que ya salio no se carga en el deposito: no esta ahi. Aceptarlo
            // dejaria la mercaderia esperando a la SIGUIENTE salida, que no es lo que
            // quiso hacer nadie.
            if (await _sessions.HasOpenSessionForVehicleAsync(vehicle.Id, null, cancellationToken))
                throw new ConflictException(
                    $"El vehiculo {vehicle.LicensePlate} esta en la calle con una salida abierta. " +
                    "Si se quedo sin stock, cargalo como recarga en ruta sobre esa salida.");

            foreach (var item in request.Items)
                if (!await _products.ExistsAsync(item.ProductId, cancellationToken))
                    throw new NotFoundException(nameof(Product), item.ProductId);

            // Cortado al microsegundo, que es lo que guarda Postgres: asi un reintento
            // devuelve exactamente la misma hora que la primera respuesta.
            var horario = await _workingHours.EnsureAllowedAsync(false, request.ConfirmOutsideWorkingHours, _currentUser, cancellationToken);
            var ahora = horario.UtcNow;
            var pending = await _loads.GetPendingAsync(vehicle.Id, cancellationToken);
            var routeDays = request.RouteDays?.Order().ToArray() ?? RouteDaySelection.Resolve(pending, ahora);
            // Toda la carga pendiente pertenece a la misma próxima salida. Al reemplazar,
            // lo anterior se baja como en Vaciar carga y queda en Cargas bajadas.
            foreach (var load in pending)
            {
                if (request.ReplacePending)
                {
                    load.RemovedByUserId = _currentUser.UserId;
                    load.IsDeleted = true;
                    load.DeletedAt = ahora;
                }
                else load.RouteDays = routeDays.ToArray();
            }

            foreach (var item in request.Items)
            {
                await _loads.AddAsync(new VehicleLoad
                {
                    VehicleId          = vehicle.Id,
                    ProductId          = item.ProductId,
                    Quantity           = item.Quantity,
                    RouteDays          = routeDays.ToArray(),
                    LoadedAt           = ahora,
                    IsOutsideWorkingHours = horario.IsOutsideWorkingHours,
                    ClientRequestId    = request.ClientRequestId,
                    RegisteredByUserId = _currentUser.UserId
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            result = new RegisterVehicleLoadResult(ahora);
        }, cancellationToken);
        return result;
    }
}
