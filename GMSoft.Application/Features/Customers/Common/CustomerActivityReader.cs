using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

public static class CustomerActivityReader
{
    /// <summary>
    /// Turnos perdidos de cada cliente pedido. Una sola consulta de salidas para
    /// todos, acotada a sus camiones y a la fecha mas vieja que hace falta mirar.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, int>> MissedWeeksAsync(
        ICustomerRepository customers,
        IReadOnlyCollection<Customer> items,
        IReadOnlyDictionary<Guid, DateTime> lastPurchases,
        CancellationToken cancellationToken)
    {
        DateTime? UltimaCompra(Customer c) => lastPurchases.TryGetValue(c.Id, out var fecha) ? fecha : null;

        var conRecorrido = items
            .Where(c => c.VehicleId is not null && c.VisitDays is { Length: > 0 })
            .ToList();

        IReadOnlyList<RouteDeparture> salidas = [];
        if (conRecorrido.Count > 0)
        {
            salidas = await customers.GetClosedDeparturesAsync(
                conRecorrido.Select(c => c.VehicleId!.Value).Distinct().ToList(),
                conRecorrido.Min(c => UltimaCompra(c) ?? c.CreatedAt),
                cancellationToken);
        }

        return items.ToDictionary(
            c => c.Id,
            c => CustomerActivityPolicy.MissedWeeks(c, UltimaCompra(c), salidas));
    }
}
