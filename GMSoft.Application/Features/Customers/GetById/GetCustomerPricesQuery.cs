using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetById;

public record CustomerPriceDto(Guid ProductId, decimal Price);
public record GetCustomerPricesQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerPriceDto>>;

public class GetCustomerPricesQueryHandler(ICustomerRepository customers, ICustomerPriceRepository prices, CustomerRouteAccess route)
    : IRequestHandler<GetCustomerPricesQuery, IReadOnlyList<CustomerPriceDto>>
{
    public async Task<IReadOnlyList<CustomerPriceDto>> Handle(GetCustomerPricesQuery request, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        await route.EnsureCanReadAsync(customer, cancellationToken);
        var rows = await prices.GetByCustomerAsync(request.CustomerId, cancellationToken);
        return rows.Select(row => new CustomerPriceDto(row.ProductId, row.Price)).ToList();
    }
}
