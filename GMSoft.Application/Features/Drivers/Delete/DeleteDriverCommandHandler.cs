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
