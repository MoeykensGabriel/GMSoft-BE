using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Products.Update;

public record UpdateProductCommand(
    Guid              Id,
    string            Detail,
    string?           CommercialDetail,
    decimal           SalePrice,
    ContainerTracking Tracking,
    bool              IsPublished,
    string?           ImageUrl) : IRequest;
