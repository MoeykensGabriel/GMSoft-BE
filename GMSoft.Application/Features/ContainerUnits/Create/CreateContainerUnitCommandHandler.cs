using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Create;

public class CreateContainerUnitCommandHandler : IRequestHandler<CreateContainerUnitCommand, Guid>
{
    private readonly IContainerUnitRepository _units;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CreateContainerUnitCommandHandler(
        IContainerUnitRepository units,
        IProductRepository products,
        IUnitOfWork unitOfWork)
    {
        _units      = units;
        _products   = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateContainerUnitCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Solo tiene sentido para lo que se sigue por numero. Un bidon con numero de
        // serie seria una unidad mas a contar dos veces: por saldo y por unidad.
        if (product.Tracking != ContainerTracking.ByUnit)
            throw new BadRequestException(
                $"'{product.Detail}' no se sigue por numero de serie. " +
                "Cambiale el modo de seguimiento a ByUnit o usa el saldo por cliente.");

        var serie = request.SerialNumber.Trim();

        if (await _units.ExistsBySerialNumberAsync(serie, null, cancellationToken))
            throw new ConflictException($"Ya existe una unidad con el numero {serie}.");

        var unit = new ContainerUnit
        {
            ProductId    = request.ProductId,
            SerialNumber = serie,
            Status       = ContainerUnitStatus.InDepot
        };

        await _units.AddAsync(unit, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}
