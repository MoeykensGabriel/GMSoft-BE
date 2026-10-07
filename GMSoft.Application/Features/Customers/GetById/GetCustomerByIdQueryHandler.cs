using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetById;

public class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly ICustomerRepository _customers;
    private readonly CustomerActivityPolicy _activityPolicy;
    private readonly CustomerRouteAccess _route;

    public GetCustomerByIdQueryHandler(ICustomerRepository customers, CustomerActivityPolicy activityPolicy, CustomerRouteAccess route)
    {
        _customers = customers;
        _activityPolicy = activityPolicy;
        _route = route;
    }

    public async Task<CustomerDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetWithZoneAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        await _route.EnsureCanReadAsync(customer, cancellationToken);

        var ultimasCompras = await _customers.GetLastPurchaseDatesAsync(
            [customer.Id], cancellationToken);

        var turnosPerdidos = await CustomerActivityReader.MissedWeeksAsync(
            _customers, [customer], ultimasCompras, cancellationToken);

        return CustomerMapping.ToDto(
            customer,
            ultimasCompras.TryGetValue(customer.Id, out var ultima) ? ultima : null,
            turnosPerdidos[customer.Id],
            _activityPolicy, DateTime.UtcNow);
    }
}
