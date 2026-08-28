using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetById;

public class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetWithZoneAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        return CustomerMapping.ToDto(customer);
    }
}
