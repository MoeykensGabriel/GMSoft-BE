using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Products.Delete;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products   = products;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        // Un producto con historia no se elimina, se despublica. Borrarlo lo sacaria
        // de las entregas viejas y del libro mayor de envases por el filtro de soft
        // delete, y esos registros quedarian sin producto. Eliminar es para el alta
        // cargada mal.
        if (await _products.HasHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Este producto ya tiene movimientos y no se puede eliminar. " +
                "Despublicalo: deja de aparecer para el reparto y el historial queda intacto.");

        _products.Delete(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
