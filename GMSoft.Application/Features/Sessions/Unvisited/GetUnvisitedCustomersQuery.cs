using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Unvisited;

public record UnvisitedCustomersDto(IReadOnlyList<CustomerDto> Items, int Total);
public record GetUnvisitedCustomersQuery(Guid Id) : IRequest<UnvisitedCustomersDto>;

public class GetUnvisitedCustomersQueryHandler(ISessionRepository sessions, ICustomerRepository customers,
    ICurrentUserService user, CustomerActivityPolicy policy)
    : IRequestHandler<GetUnvisitedCustomersQuery, UnvisitedCustomersDto>
{
    public async Task<UnvisitedCustomersDto> Handle(GetUnvisitedCustomersQuery request, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        if (!user.IsInRole(AppRoles.Admin) && user.DriverId != session.DriverId)
            throw new ForbiddenException("Esta salida es de otro chofer.");
        // Muestra los clientes actuales del recorrido: con la salida cerrada, el chofer
        // ya no tiene por qué verlos (pudieron pasar a otro camión). ADMIN sí, para revisar.
        if (!user.IsInRole(AppRoles.Admin) && session.Status != SessionStatus.Open)
            throw new ForbiddenException("La salida ya se cerró.");
        // Zona por zona, en el orden en que la salida las recorre.
        var route = new List<Customer>();
        foreach (var zoneId in await sessions.GetZoneIdsAsync(session.Id, ct) is { Count: > 0 } zonas ? zonas : [session.ZoneId])
            route.AddRange((await customers.GetPagedAsync(1, int.MaxValue, null, zoneId, true, null,
                ct, CustomerRouteAccess.Days(session), session.VehicleId)).Items);
        var visited = (await sessions.GetDeliveriesAsync(session.Id, ct)).Select(d => d.CustomerId).ToHashSet();
        var items = route.Where(c => !visited.Contains(c.Id)).ToList();
        var purchases = await customers.GetLastPurchaseDatesAsync(items.Select(c => c.Id).ToList(), ct);
        var weeks = await CustomerActivityReader.MissedWeeksAsync(customers, items, purchases, ct);
        var now = DateTime.UtcNow;
        var result = items.Select(c => CustomerMapping.ToDto(c,
            purchases.TryGetValue(c.Id, out var date) ? date : null, weeks[c.Id], policy, now)).ToList();
        return new(result, result.Count);
    }
}
