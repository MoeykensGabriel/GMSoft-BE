using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.Update;

public class UpdateDriverCommandHandler : IRequestHandler<UpdateDriverCommand>
{
    private readonly IDriverRepository _drivers;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverCommandHandler(
        IDriverRepository drivers,
        IRepository<Vehicle> vehicles,
        IIdentityService identityService,
        IUnitOfWork unitOfWork)
    {
        _drivers         = drivers;
        _vehicles        = vehicles;
        _identityService = identityService;
        _unitOfWork      = unitOfWork;
    }

    public async Task Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        if (await _drivers.ExistsByDocumentAsync(request.DocumentNumber, request.Id, cancellationToken))
            throw new ConflictException($"Ya hay otro chofer con el documento {request.DocumentNumber}.");

        if (request.VehicleId is not null &&
            !await _vehicles.ExistsAsync(request.VehicleId.Value, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.VehicleId.Value);

        var cambiaActividad = driver.IsActive != request.IsActive;

        driver.FirstName      = request.FirstName.Trim();
        driver.LastName       = request.LastName.Trim();
        driver.DocumentNumber = request.DocumentNumber.Trim();
        driver.Phone          = request.Phone.Trim();
        driver.VehicleId      = request.VehicleId;
        driver.IsActive       = request.IsActive;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _drivers.Update(driver);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Desactivar la ficha tiene que cerrarle el acceso: si no, el chofer
            // desaparece de las listas y sigue entrando a cargar entregas.
            if (cambiaActividad && driver.ApplicationUserId is not null)
                await _identityService.SetUserActiveAsync(
                    driver.ApplicationUserId.Value, request.IsActive, cancellationToken);
        }, cancellationToken);
    }
}
