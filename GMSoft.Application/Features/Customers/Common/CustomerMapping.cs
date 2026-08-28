using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

public static class CustomerMapping
{
    public static CustomerDto ToDto(Customer customer, DateTime? lastPurchaseAt = null)
    {
        // Se cuenta en dias enteros contra hoy. Un cliente que compro hace unas horas
        // da 0, no 1, que es lo que espera leer alguien mirando la lista.
        int? diasSinComprar = lastPurchaseAt is null
            ? null
            : Math.Max(0, (int)(DateTime.UtcNow.Date - lastPurchaseAt.Value.Date).TotalDays);

        return new CustomerDto(
            Id:                  customer.Id,
            BusinessName:        customer.BusinessName,
            ContactName:         customer.ContactName,
            Phone:               customer.Phone,
            Address:             customer.Address,
            Email:               customer.Email,
            ZoneId:              customer.ZoneId,
            ZoneName:            customer.Zone?.Name,
            RouteOrder:          customer.RouteOrder,
            Notes:               customer.Notes,
            IsActive:            customer.IsActive,
            LastPurchaseAt:      lastPurchaseAt,
            DaysWithoutPurchase: diasSinComprar);
    }
}
