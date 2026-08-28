using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Decommission;

public class DecommissionContainerUnitCommandHandler
    : IRequestHandler<DecommissionContainerUnitCommand>
{
    private readonly IContainerUnitRepository _units;
    private readonly IRepository<ContainerMovement> _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DecommissionContainerUnitCommandHandler(
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

    public async Task Handle(
        DecommissionContainerUnitCommand request,
        CancellationToken cancellationToken)
    {
        var unit = await _units.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ContainerUnit), request.Id);

        if (unit.Status == ContainerUnitStatus.OutOfService)
            throw new ConflictException($"La unidad {unit.SerialNumber} ya esta fuera de servicio.");

        // Si estaba en poder de un cliente, sale de ahi. Dar de baja sin descontarla
        // la dejaria figurando como reclamable a alguien que ya no la tiene.
        var clienteQueLaTenia = unit.CurrentCustomerId;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            unit.Status            = ContainerUnitStatus.OutOfService;
            unit.CurrentCustomerId = null;
            _units.Update(unit);

            await _movements.AddAsync(new ContainerMovement
            {
                ProductId          = unit.ProductId,
                CustomerId         = clienteQueLaTenia,
                ContainerUnitId    = unit.Id,
                Quantity           = clienteQueLaTenia is null ? 0 : -1,
                Type               = ContainerMovementType.Lost,
                OccurredAt         = DateTime.UtcNow,
                RegisteredByUserId = _currentUser.UserId,
                Notes              = request.Reason.Trim()
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
