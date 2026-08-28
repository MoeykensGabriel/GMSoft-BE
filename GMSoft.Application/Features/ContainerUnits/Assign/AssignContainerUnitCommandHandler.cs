using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Assign;

public class AssignContainerUnitCommandHandler : IRequestHandler<AssignContainerUnitCommand>
{
    private readonly IContainerUnitRepository _units;
    private readonly ICustomerRepository _customers;
    private readonly IRepository<ContainerMovement> _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AssignContainerUnitCommandHandler(
        IContainerUnitRepository units,
        ICustomerRepository customers,
        IRepository<ContainerMovement> movements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _units       = units;
        _customers   = customers;
        _movements   = movements;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task Handle(AssignContainerUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _units.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ContainerUnit), request.Id);

        // Una unidad ya entregada no se puede entregar de nuevo: quedaria figurando en
        // dos clientes y el reclamo iria al equivocado. Primero se recupera.
        if (unit.Status == ContainerUnitStatus.WithCustomer)
            throw new ConflictException(
                $"La unidad {unit.SerialNumber} ya esta en poder de un cliente. Recuperala primero.");

        if (unit.Status == ContainerUnitStatus.OutOfService)
            throw new ConflictException($"La unidad {unit.SerialNumber} esta fuera de servicio.");

        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (!customer.IsActive)
            throw new ConflictException("El cliente esta desactivado.");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            unit.Status            = ContainerUnitStatus.WithCustomer;
            unit.CurrentCustomerId = customer.Id;
            _units.Update(unit);

            // Asienta en el mismo libro mayor que los bidones: los envases se mueven
            // en una sola tabla, se sigan por saldo o por numero.
            await _movements.AddAsync(new ContainerMovement
            {
                ProductId          = unit.ProductId,
                CustomerId         = customer.Id,
                ContainerUnitId    = unit.Id,
                Quantity           = 1,
                Type               = ContainerMovementType.DeliveredToCustomer,
                OccurredAt         = DateTime.UtcNow,
                RegisteredByUserId = _currentUser.UserId,
                Notes              = request.Notes?.Trim()
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
