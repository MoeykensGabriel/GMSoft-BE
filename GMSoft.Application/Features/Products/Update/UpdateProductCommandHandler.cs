using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Products.Update;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products   = products;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        if (await _products.ExistsByDetailAsync(request.Detail, request.Id, cancellationToken))
            throw new ConflictException($"Ya existe otro producto con el detalle '{request.Detail}'.");

        // Cambiar como se sigue un producto que ya tiene envases en la calle deja los
        // datos viejos contados de una forma que el producto ya no usa: los saldos por
        // cantidad quedan huerfanos al pasar a numero de serie, y las unidades
        // numeradas al reves. No falla nada en el momento, y despues no se sabe cuantos
        // envases hay realmente afuera.
        if (product.Tracking != request.Tracking &&
            await _products.HasContainerHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Este producto ya tiene envases en circulacion y no se puede cambiar su modo de " +
                "seguimiento. Si necesitas seguirlo distinto, da de alta un producto nuevo y " +
                "despublica este.");

        product.Detail           = request.Detail.Trim();
        product.CommercialDetail = request.CommercialDetail?.Trim();
        product.SalePrice        = request.SalePrice;
        product.Tracking         = request.Tracking;
        product.IsPublished      = request.IsPublished;
        product.ImageUrl         = request.ImageUrl;

        _products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
