using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Register;

public class RegisterVehicleLoadCommandHandler : IRequestHandler<RegisterVehicleLoadCommand>
{
    private readonly IVehicleLoadRepository _loads;
    private readonly IVehicleRepository _vehicles;
    private readonly ISessionRepository _sessions;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterVehicleLoadCommandHandler(
        IVehicleLoadRepository loads,
        IVehicleRepository vehicles,
        ISessionRepository sessions,
        IRepository<Product> products,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _loads       = loads;
        _vehicles    = vehicles;
        _sessions    = sessions;
        _products    = products;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task Handle(RegisterVehicleLoadCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);

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

        var ahora = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            await _loads.AddAsync(new VehicleLoad
            {
                VehicleId          = vehicle.Id,
                ProductId          = item.ProductId,
                Quantity           = item.Quantity,
                LoadedAt           = ahora,
                RegisteredByUserId = _currentUser.UserId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
