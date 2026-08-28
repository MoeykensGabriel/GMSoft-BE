using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

public static class CustomerMapping
{
    public static CustomerDto ToDto(Customer customer) => new(
        Id:           customer.Id,
        BusinessName: customer.BusinessName,
        ContactName:  customer.ContactName,
        Phone:        customer.Phone,
        Address:      customer.Address,
        Email:        customer.Email,
        ZoneId:       customer.ZoneId,
        ZoneName:     customer.Zone?.Name,
        RouteOrder:   customer.RouteOrder,
        Notes:        customer.Notes,
        IsActive:     customer.IsActive);
}
