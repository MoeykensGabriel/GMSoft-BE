using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Clear;

/// <summary>
/// Vaciar el camión: baja de una vez toda la carga que todavía no salió. Devuelve
/// cuántos renglones bajó. Lo que ya salió con una salida no se toca.
/// </summary>
public record ClearVehicleLoadCommand(Guid VehicleId) : IRequest<int>;

public class ClearVehicleLoadCommandHandler(
    IVehicleLoadRepository loads,
    IVehicleRepository vehicles,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<ClearVehicleLoadCommand, int>
{
    public async Task<int> Handle(ClearVehicleLoadCommand request, CancellationToken cancellationToken)
    {
        var count = 0;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (await vehicles.LockAsync(request.VehicleId, cancellationToken) is null)
                throw new NotFoundException(nameof(Vehicle), request.VehicleId);

            var pending = await loads.GetPendingAsync(request.VehicleId, cancellationToken);
            foreach (var load in pending)
            {
                load.RemovedByUserId = currentUser.UserId;
                load.IsDeleted = true;
                load.DeletedAt = DateTime.UtcNow;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            count = pending.Count;
        }, cancellationToken);
        return count;
    }
}
