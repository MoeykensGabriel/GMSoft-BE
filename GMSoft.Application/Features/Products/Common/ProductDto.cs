using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Products.Common;

public record ProductDto(
    Guid              Id,
    string            Detail,
    string?           CommercialDetail,
    decimal           SalePrice,
    ContainerTracking Tracking,
    bool              IsPublished,
    string?           ImageUrl);
