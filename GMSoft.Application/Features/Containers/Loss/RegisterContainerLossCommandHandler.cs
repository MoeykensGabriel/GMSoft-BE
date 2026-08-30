using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Containers.Loss;

public class RegisterContainerLossCommandHandler : IRequestHandler<RegisterContainerLossCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IContainerBalanceRepository _balances;
    private readonly IRepository<ContainerMovement> _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterContainerLossCommandHandler(
        ICustomerRepository customers,
        IProductRepository products,
        IContainerBalanceRepository balances,
        IRepository<ContainerMovement> movements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _customers   = customers;
        _products    = products;
        _balances    = balances;
        _movements   = movements;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task Handle(RegisterContainerLossCommand request, CancellationToken cancellationToken)
    {
        if (!await _customers.ExistsAsync(request.CustomerId, cancellationToken))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (product.Tracking != ContainerTracking.ByBalance)
            throw new BadRequestException(
                $"'{product.Detail}' no se sigue por saldo. " +
                "Los envases con numero de serie se dan de baja unidad por unidad.");

        var saldo    = await _balances.GetAsync(request.CustomerId, request.ProductId, cancellationToken);
        var enPoder  = saldo?.Quantity ?? 0;

        // No se puede perder mas de lo que tenia. Dejarlo pasar mandaria el saldo a
        // negativo, y un cliente con -2 bidones es un numero que no significa nada y
        // ensucia el total de envases en la calle.
        if (request.Quantity > enPoder)
            throw new ConflictException(
                $"El cliente tiene {enPoder} de '{product.Detail}' y se quieren dar por perdidos " +
                $"{request.Quantity}. Si el saldo esta mal, corregilo con un ajuste.");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _movements.AddAsync(new ContainerMovement
            {
                ProductId          = request.ProductId,
                CustomerId         = request.CustomerId,
                Quantity           = -request.Quantity,
                Type               = ContainerMovementType.Lost,
                OccurredAt         = DateTime.UtcNow,
                RegisteredByUserId = _currentUser.UserId,
                Notes              = request.Reason.Trim()
            }, cancellationToken);

            await _balances.AdjustAsync(
                request.CustomerId, request.ProductId, -request.Quantity, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
