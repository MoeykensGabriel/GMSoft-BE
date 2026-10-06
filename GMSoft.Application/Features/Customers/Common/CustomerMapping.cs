using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

public static class CustomerMapping
{
    public static CustomerDto ToDto(Customer customer, DateTime? lastPurchaseAt,
        CustomerActivityPolicy activityPolicy, DateTime nowUtc)
    {
        // Se cuenta en dias enteros contra hoy. Un cliente que compro hace unas horas
        // da 0, no 1, que es lo que espera leer alguien mirando la lista.
        int? diasSinComprar = lastPurchaseAt is null
            ? null
            : CustomerActivityPolicy.DaysSince(lastPurchaseAt.Value, nowUtc);

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
            DaysWithoutPurchase: diasSinComprar)
        {
            VisitDays = customer.VisitDays,
            VehicleId = customer.VehicleId,
            VehicleName = customer.Vehicle?.Name,
            VehicleLicensePlate = customer.Vehicle?.LicensePlate,
            LastVisitAt = customer.LastVisitAt,
            ActivityStatus = activityPolicy.GetStatus(
                diasSinComprar ?? CustomerActivityPolicy.DaysSince(customer.CreatedAt, nowUtc))
        };
    }
}
