using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Products.Create;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products   = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await _products.ExistsByDetailAsync(request.Detail, null, cancellationToken))
            throw new ConflictException($"Ya existe un producto con el detalle '{request.Detail}'.");

        var product = new Product
        {
            Detail           = request.Detail.Trim(),
            CommercialDetail = request.CommercialDetail?.Trim(),
            SalePrice        = request.SalePrice,
            Tracking         = request.Tracking,
            IsPublished      = request.IsPublished,
            ImageUrl         = request.ImageUrl
        };

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // El Id lo asigna SaveChangesAsync, nunca se pone a mano.
        return product.Id;
    }
}
