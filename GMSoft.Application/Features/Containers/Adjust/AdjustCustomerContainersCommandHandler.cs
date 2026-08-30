using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Containers.Adjust;

public class AdjustCustomerContainersCommandHandler
    : IRequestHandler<AdjustCustomerContainersCommand, AdjustCustomerContainersResult>
{
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IContainerBalanceRepository _balances;
    private readonly IRepository<ContainerMovement> _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AdjustCustomerContainersCommandHandler(
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

    public async Task<AdjustCustomerContainersResult> Handle(
        AdjustCustomerContainersCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _customers.ExistsAsync(request.CustomerId, cancellationToken))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (product.Tracking != ContainerTracking.ByBalance)
            throw new BadRequestException(
                $"'{product.Detail}' no se sigue por saldo. " +
                "Los envases con numero de serie se corrigen unidad por unidad.");

        var saldo    = await _balances.GetAsync(request.CustomerId, request.ProductId, cancellationToken);
        var anterior = saldo?.Quantity ?? 0;
        var delta    = request.RealQuantity - anterior;

        // Si el conteo coincide no se escribe nada: un movimiento de cero no explica
        // ningun cambio y solo ensucia el historial que se mira para entender un saldo.
        if (delta == 0)
            return new AdjustCustomerContainersResult(anterior, anterior, 0);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _movements.AddAsync(new ContainerMovement
            {
                ProductId          = request.ProductId,
                CustomerId         = request.CustomerId,
                Quantity           = delta,
                Type               = ContainerMovementType.Adjustment,
                OccurredAt         = DateTime.UtcNow,
                RegisteredByUserId = _currentUser.UserId,
                Notes              = request.Reason.Trim()
            }, cancellationToken);

            await _balances.AdjustAsync(
                request.CustomerId, request.ProductId, delta, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return new AdjustCustomerContainersResult(anterior, request.RealQuantity, delta);
    }
}
