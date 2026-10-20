using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Products.Create;

public record CreateProductCommand(
    string            Detail,
    string?           CommercialDetail,
    decimal           SalePrice,
    ContainerTracking Tracking,
    bool              IsPublished,
    string?           ImageUrl,
    bool              CanBeLent = false,
    string?           ExchangeGroup = null) : IRequest<Guid>;
