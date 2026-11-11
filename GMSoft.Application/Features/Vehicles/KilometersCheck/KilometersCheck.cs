using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.KilometersCheck;

/// <summary>
/// Una lectura del tablero para que la oficina la revise: qué leyó el chofer y qué
/// tenía el sistema.
/// </summary>
public record KilometersReadingDto(Guid Id, Guid VehicleId, string VehicleName, string LicensePlate,
    string DriverName, Guid SessionId, int Kilometers, int PreviousKilometers, DateTime ReadAt);

public record GetPendingKilometersReadingsQuery : IRequest<IReadOnlyList<KilometersReadingDto>>;

/// <summary>
/// Aceptar: el km pasa a ser el real del camión y desde esa salida cuentan los promedios.
/// Rechazar: en la próxima salida se le pide leer el tablero de nuevo (y esa vez puede poner menos).
/// </summary>
public record ResolveKilometersReadingCommand(Guid Id, bool Accept) : IRequest;

public class GetPendingKilometersReadingsQueryHandler(IVehicleRepository vehicles, ICurrentUserService currentUser)
    : IRequestHandler<GetPendingKilometersReadingsQuery, IReadOnlyList<KilometersReadingDto>>
{
    public async Task<IReadOnlyList<KilometersReadingDto>> Handle(GetPendingKilometersReadingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Las lecturas de kilometraje las revisa la oficina.");
        return await vehicles.GetPendingKilometersReadingsAsync(cancellationToken);
    }
}

public class ResolveKilometersReadingCommandHandler(
    IVehicleRepository vehicles, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    : IRequestHandler<ResolveKilometersReadingCommand>
{
    public async Task Handle(ResolveKilometersReadingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Las lecturas de kilometraje las revisa la oficina.");

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var reading = await vehicles.GetKilometersReadingAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(VehicleKilometersReading), request.Id);
            // Comparte el bloqueo con la apertura: la marca no cambia con una salida abriéndose.
            var vehicle = await vehicles.LockAsync(reading.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), reading.VehicleId);
            if (reading.Status != VehicleKilometersReadingStatus.Pending)
                throw new ConflictException("Esa lectura ya se revisó.");

            reading.Status = request.Accept ? VehicleKilometersReadingStatus.Accepted : VehicleKilometersReadingStatus.Rejected;
            reading.ResolvedAt = DateTime.UtcNow;
            reading.ResolvedByUserId = currentUser.UserId;
            if (request.Accept) vehicle.KilometersCheckedAt = reading.ReadAt;
            else vehicle.KilometersCheckPending = true;
            vehicles.Update(vehicle);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
