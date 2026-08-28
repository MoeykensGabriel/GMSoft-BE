using GMSoft.Application.Features.Products.Common;
using MediatR;

namespace GMSoft.Application.Features.Products.GetById;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
