using FluentValidation;
using GMSoft.Application.Common.Validation;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

public class NewCustomerLineValidator : AbstractValidator<NewCustomerLine>
{
    public NewCustomerLineValidator()
    {
        RuleFor(x => x.VisitDays).ValidVisitDays();
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.BusinessName).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public static class StreetCustomer
{
    public static async Task<Customer> CreateAsync(NewCustomerLine data, Guid vehicleId, Guid zoneId,
        ICustomerRepository customers, CancellationToken ct)
    {
        var customer = new Customer
        {
            BusinessName = string.IsNullOrWhiteSpace(data.BusinessName) ? null : data.BusinessName.Trim(),
            ContactName = data.ContactName.Trim(), Phone = data.Phone.Trim(), Address = data.Address.Trim(),
            Notes = data.Notes?.Trim(), ZoneId = zoneId, VehicleId = vehicleId,
            VisitDays = data.VisitDays!.Order().ToArray(), IsActive = true,
            RouteOrder = await customers.GetNextRouteOrderAsync(zoneId, ct)
        };
        await customers.AddAsync(customer, ct);
        return customer;
    }
}

