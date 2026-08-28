using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetList;

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ICustomerRepository _customers;

    public GetCustomersQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<PagedResult<CustomerDto>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _customers.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.ZoneId,
            request.OnlyActive,
            request.InactiveSinceDays,
            cancellationToken);

        // Una sola consulta para las ultimas compras de toda la pagina, en vez de
        // una por cliente.
        var ultimasCompras = await _customers.GetLastPurchaseDatesAsync(
            items.Select(c => c.Id).ToList(), cancellationToken);

        return new PagedResult<CustomerDto>(
            items.Select(c => CustomerMapping.ToDto(
                c,
                ultimasCompras.TryGetValue(c.Id, out var ultima) ? ultima : null)).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
