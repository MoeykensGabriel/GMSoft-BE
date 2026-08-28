using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Create;

public class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, Guid>
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVehicleCommandHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork)
    {
        _vehicles   = vehicles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var plate = Normalizar(request.LicensePlate);

        if (await _vehicles.ExistsByLicensePlateAsync(plate, null, cancellationToken))
            throw new ConflictException($"Ya hay un vehiculo con la patente {plate}.");

        var vehicle = new Vehicle
        {
            Name              = request.Name.Trim(),
            LicensePlate      = plate,
            Type              = request.Type,
            CurrentKilometers = request.CurrentKilometers
        };

        await _vehicles.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.Id;
    }

    /// <summary>
    /// La patente se guarda sin espacios y en mayusculas. Si no, "ab 123 cd" y
    /// "AB123CD" entran como dos vehiculos distintos y el unico no lo impide.
    /// </summary>
    internal static string Normalizar(string licensePlate)
        => licensePlate.Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
}
