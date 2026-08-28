using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Vehicles.Create;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Update;

public class UpdateVehicleCommandHandler : IRequestHandler<UpdateVehicleCommand>
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVehicleCommandHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork)
    {
        _vehicles   = vehicles;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.Id);

        var plate = CreateVehicleCommandHandler.Normalizar(request.LicensePlate);

        if (await _vehicles.ExistsByLicensePlateAsync(plate, request.Id, cancellationToken))
            throw new ConflictException($"Ya hay otro vehiculo con la patente {plate}.");

        // El kilometraje no puede ir para atras: lo cargan los choferes al abrir
        // sesion y un numero menor al actual es siempre un error de tipeo.
        if (request.CurrentKilometers < vehicle.CurrentKilometers)
            throw new BadRequestException(
                $"El kilometraje no puede bajar. El vehiculo tiene {vehicle.CurrentKilometers} km.");

        vehicle.Name              = request.Name.Trim();
        vehicle.LicensePlate      = plate;
        vehicle.Type              = request.Type;
        vehicle.CurrentKilometers = request.CurrentKilometers;

        _vehicles.Update(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
