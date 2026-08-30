using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.Create;

public class CreateDriverCommandHandler : IRequestHandler<CreateDriverCommand, Guid>
{
    private readonly IDriverRepository _drivers;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDriverCommandHandler(
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

    public async Task<Guid> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        if (await _drivers.ExistsByDocumentAsync(request.DocumentNumber, null, cancellationToken))
            throw new ConflictException($"Ya hay un chofer con el documento {request.DocumentNumber}.");

        if (request.VehicleId is not null &&
            !await _vehicles.ExistsAsync(request.VehicleId.Value, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.VehicleId.Value);

        var driverId = Guid.Empty;

        // La cuenta y la ficha van juntas o no van. Sin transaccion, si falla la
        // segunda queda un usuario que puede entrar al sistema sin ser nadie.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var userId = await _identityService.CreateUserAsync(
                request.UserName,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                AppRoles.Driver,
                cancellationToken);

            var driver = new Driver
            {
                FirstName         = request.FirstName.Trim(),
                LastName          = request.LastName.Trim(),
                DocumentNumber    = request.DocumentNumber.Trim(),
                Phone             = request.Phone.Trim(),
                VehicleId         = request.VehicleId,
                ApplicationUserId = userId,
                IsActive          = true
            };

            await _drivers.AddAsync(driver, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            driverId = driver.Id;
        }, cancellationToken);

        return driverId;
    }
}
