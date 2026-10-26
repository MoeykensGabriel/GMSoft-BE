using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Vehicles.Create;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Update;

public class UpdateVehicleCommandHandler(
    IVehicleRepository vehicles, ISessionRepository sessions, ICurrentUserService currentUser,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<UpdateVehicleCommand>
{
    public async Task Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("El kilometraje lo corrige la oficina.");

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Comparte el bloqueo con la apertura de salidas: la base no cambia en el medio.
            var vehicle = await vehicles.LockAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), request.Id);
            var plate = CreateVehicleCommandHandler.Normalizar(request.LicensePlate);
            if (await vehicles.ExistsByLicensePlateAsync(plate, request.Id, cancellationToken))
                throw new ConflictException($"Ya hay otro vehiculo con la patente {plate}.");

            if (request.CurrentKilometers < vehicle.CurrentKilometers)
            {
                if (string.IsNullOrWhiteSpace(request.KilometersReason))
                    throw new BadRequestException("Para bajar el kilometraje escribí el motivo.");
                if (await sessions.HasOpenSessionForVehicleAsync(vehicle.Id, null, cancellationToken))
                    throw new ConflictException("El camión está en la calle: corregí el kilometraje cuando lo recibas.");

                await vehicles.AddKilometersCorrectionAsync(new VehicleKilometersCorrection
                {
                    VehicleId = vehicle.Id,
                    PreviousKilometers = vehicle.CurrentKilometers,
                    NewKilometers = request.CurrentKilometers,
                    Reason = request.KilometersReason.Trim(),
                    CorrectedAt = clock.GetUtcNow().UtcDateTime,
                    CorrectedByUserId = currentUser.UserId
                }, cancellationToken);
            }

            vehicle.Name = request.Name.Trim();
            vehicle.LicensePlate = plate;
            vehicle.Type = request.Type;
            vehicle.CurrentKilometers = request.CurrentKilometers;
            vehicles.Update(vehicle);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
