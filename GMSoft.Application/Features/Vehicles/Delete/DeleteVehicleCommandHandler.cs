using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Delete;

public class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand>
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVehicleCommandHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork)
    {
        _vehicles   = vehicles;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.Id);

        // Un vehiculo que ya salio a repartir no se elimina: sus sesiones quedarian
        // sin poder decir con que camion se hicieron.
        if (await _vehicles.HasHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Este vehiculo ya tiene sesiones de reparto y no se puede eliminar.");

        _vehicles.Delete(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
