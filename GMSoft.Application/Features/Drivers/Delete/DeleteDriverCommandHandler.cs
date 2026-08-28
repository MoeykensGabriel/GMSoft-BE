using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.Delete;

public class DeleteDriverCommandHandler : IRequestHandler<DeleteDriverCommand>
{
    private readonly IDriverRepository _drivers;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDriverCommandHandler(
        IDriverRepository drivers,
        IIdentityService identityService,
        IUnitOfWork unitOfWork)
    {
        _drivers         = drivers;
        _identityService = identityService;
        _unitOfWork      = unitOfWork;
    }

    public async Task Handle(DeleteDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        // Un chofer que ya salio a repartir no se elimina, se desactiva. El filtro
        // de soft delete lo sacaria tambien de sus propias sesiones, y las salidas
        // viejas quedarian sin dueño. Eliminar es para el alta cargada mal.
        if (await _drivers.HasHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Este chofer ya tiene sesiones de reparto y no se puede eliminar. " +
                "Desactivalo: la ficha queda, el historial se sigue viendo y pierde el acceso igual.");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Soft delete: sus sesiones y entregas lo siguen referenciando.
            _drivers.Delete(driver);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Y se le cierra el acceso. Borrar la ficha sin desactivar la cuenta
            // deja entrando al sistema a alguien que ya no trabaja en el negocio.
            if (driver.ApplicationUserId is not null)
                await _identityService.SetUserActiveAsync(
                    driver.ApplicationUserId.Value, false, cancellationToken);
        }, cancellationToken);
    }
}
