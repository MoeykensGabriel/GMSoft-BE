using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetList;

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ICustomerRepository _customers;
    private readonly CustomerActivityPolicy _activityPolicy;
    private readonly CustomerRouteAccess _route;

    public GetCustomersQueryHandler(ICustomerRepository customers, CustomerActivityPolicy activityPolicy, CustomerRouteAccess route)
    {
        _customers = customers;
        _activityPolicy = activityPolicy;
        _route = route;
    }

    public async Task<PagedResult<CustomerDto>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _route.GetDriverSessionAsync(cancellationToken);
        var (items, totalCount) = await _customers.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            session?.ZoneId ?? request.ZoneId,
            session is not null ? true : request.OnlyActive,
            request.InactiveSinceDays,
            cancellationToken,
            session is not null ? CustomerRouteAccess.Days(session)
                : request.VisitDays ?? (request.TodayOnly ? [BusinessTime.IsoDayOfWeek(DateTime.UtcNow)] : null),
            session?.VehicleId ?? request.VehicleId);

        // Una sola consulta para las ultimas compras de toda la pagina, en vez de
        // una por cliente.
        var ultimasCompras = await _customers.GetLastPurchaseDatesAsync(
            items.Select(c => c.Id).ToList(), cancellationToken);

        // Y otra sola para las salidas que deciden los turnos perdidos de la pagina.
        var turnosPerdidos = await CustomerActivityReader.MissedWeeksAsync(
            _customers, items, ultimasCompras, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        return new PagedResult<CustomerDto>(
            items.Select(c => CustomerMapping.ToDto(
                c,
                ultimasCompras.TryGetValue(c.Id, out var ultima) ? ultima : null,
                turnosPerdidos[c.Id],
                _activityPolicy, nowUtc)).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
