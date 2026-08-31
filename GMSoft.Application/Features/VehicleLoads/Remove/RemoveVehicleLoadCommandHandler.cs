using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Remove;

public class RemoveVehicleLoadCommandHandler : IRequestHandler<RemoveVehicleLoadCommand>
{
    private readonly IVehicleLoadRepository _loads;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveVehicleLoadCommandHandler(IVehicleLoadRepository loads, IUnitOfWork unitOfWork)
    {
        _loads      = loads;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoveVehicleLoadCommand request, CancellationToken cancellationToken)
    {
        var load = await _loads.GetByIdAsync(request.LoadId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleLoad), request.LoadId);

        // El vehiculo viene de la ruta: sin esta comprobacion se podria bajar una
        // carga de otro camion sabiendo solo el id de la linea.
        if (load.VehicleId != request.VehicleId)
            throw new NotFoundException(nameof(VehicleLoad), request.LoadId);

        // Una carga que ya salio es historia: se convirtio en la carga inicial de una
        // salida, y ese stock ya se vendio o se descargo. Borrarla aca dejaria a la
        // salida diciendo que subio algo que nunca existio.
        if (load.ConsumedBySessionId is not null)
            throw new ConflictException(
                "Esa carga ya salio con una salida de reparto y no se puede bajar. " +
                "Si el numero esta mal, corregilo como ajuste sobre esa salida.");

        _loads.Delete(load);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
