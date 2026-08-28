using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.ContainerUnits.Common;

public static class ContainerUnitMapping
{
    public static ContainerUnitDto ToDto(ContainerUnit unit) => new(
        Id:                  unit.Id,
        ProductId:           unit.ProductId,
        ProductDetail:       unit.Product?.Detail ?? string.Empty,
        SerialNumber:        unit.SerialNumber,
        Status:              unit.Status,
        CurrentCustomerId:   unit.CurrentCustomerId,
        CurrentCustomerName: unit.CurrentCustomer is null
            ? null
            : string.IsNullOrWhiteSpace(unit.CurrentCustomer.BusinessName)
                ? unit.CurrentCustomer.ContactName
                : unit.CurrentCustomer.BusinessName);
}
