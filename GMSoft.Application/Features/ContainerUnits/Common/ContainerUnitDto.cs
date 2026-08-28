using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.ContainerUnits.Common;

public record ContainerUnitDto(
    Guid                Id,
    Guid                ProductId,
    string              ProductDetail,
    string              SerialNumber,
    ContainerUnitStatus Status,
    Guid?               CurrentCustomerId,
    string?             CurrentCustomerName);
