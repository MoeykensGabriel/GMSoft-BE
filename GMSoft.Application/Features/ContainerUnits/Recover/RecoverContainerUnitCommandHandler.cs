using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Recover;

public class RecoverContainerUnitCommandHandler : IRequestHandler<RecoverContainerUnitCommand>
{
    private readonly IContainerUnitRepository _units;
    private readonly IRepository<ContainerMovement> _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RecoverContainerUnitCommandHandler(
        IContainerUnitRepository units,
        IRepository<ContainerMovement> movements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _units       = units;
        _movements   = movements;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task Handle(RecoverContainerUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _units.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ContainerUnit), request.Id);

        if (unit.Status != ContainerUnitStatus.WithCustomer || unit.CurrentCustomerId is null)
            throw new ConflictException(
                $"La unidad {unit.SerialNumber} no esta en poder de ningun cliente.");

        var clienteQueLaTenia = unit.CurrentCustomerId.Value;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            unit.Status            = ContainerUnitStatus.InDepot;
            unit.CurrentCustomerId = null;
            _units.Update(unit);

            await _movements.AddAsync(new ContainerMovement
            {
                ProductId          = unit.ProductId,
                CustomerId         = clienteQueLaTenia,
                ContainerUnitId    = unit.Id,
                Quantity           = -1,
                Type               = ContainerMovementType.ReturnedFromCustomer,
                OccurredAt         = DateTime.UtcNow,
                RegisteredByUserId = _currentUser.UserId,
                Notes              = request.Notes?.Trim()
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
