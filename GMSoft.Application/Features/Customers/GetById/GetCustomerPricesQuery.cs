using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetById;

public record CustomerPriceDto(Guid ProductId, decimal Price);
public record GetCustomerPricesQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerPriceDto>>;

public class GetCustomerPricesQueryHandler(ICustomerRepository customers, ICustomerPriceRepository prices)
    : IRequestHandler<GetCustomerPricesQuery, IReadOnlyList<CustomerPriceDto>>
{
    public async Task<IReadOnlyList<CustomerPriceDto>> Handle(GetCustomerPricesQuery request, CancellationToken cancellationToken)
    {
        _ = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        var rows = await prices.GetByCustomerAsync(request.CustomerId, cancellationToken);
        return rows.Select(row => new CustomerPriceDto(row.ProductId, row.Price)).ToList();
    }
}
