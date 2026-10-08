using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;

namespace ActivitySimulation;

/// <summary><c>Seeded</c> false: the customer's real last sale already gives the target; nothing is written for it.</summary>
public sealed record PlannedCustomer(Customer Customer, DateTime SaleAt, int Expected, bool Seeded = true);
public sealed record PlannedTrip(Guid ZoneId, DateTime OpenedAt, DateTime ClosedAt, int Day);
public sealed record ActivityPlan(PlannedCustomer[] Customers, PlannedTrip[] Trips);

public static class Planner
{
    public static DateTime Utc(DateOnly date, int hour) =>
        new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, 0)), BusinessTime.Offset).UtcDateTime;

    public static ActivityPlan Create(DateTime nowUtc, Customer[] customers) =>
        Create(nowUtc, customers, [], new Dictionary<Guid, DateTime>(), new HashSet<DateOnly>());

    /// <summary>
    /// Plans around what the database already has: <paramref name="real"/> are received trips of
    /// the vehicle (they count as turns), <paramref name="purchases"/> the real last sale per
    /// customer and <paramref name="occupied"/> the days a real trip already uses.
    /// </summary>
    public static ActivityPlan Create(DateTime nowUtc, Customer[] customers, IReadOnlyCollection<RouteDeparture> real,
        IReadOnlyDictionary<Guid, DateTime> purchases, IReadOnlySet<DateOnly> occupied)
    {
        if (customers.Length != 3 || customers.Select(c => c.Id).Distinct().Count() != 3)
            throw new InvalidOperationException("Se necesitan tres clientes distintos, en orden A/B/C.");
        if (customers.Any(c => c.VehicleId is null || c.VisitDays is not { Length: > 0 }
            || c.VisitDays.Any(d => d is < 1 or > 7)))
            throw new InvalidOperationException("Los tres clientes deben tener vehículo y días ISO válidos.");
        var monday = BusinessTime.WeekStart(nowUtc);
        var vehicle = customers[0].VehicleId!.Value;
        int[] targets = [0, 3, 5];
        // A trip fits a date only once it could already be received and no real trip uses that day.
        bool Free(DateOnly date) => Utc(date, 16) <= nowUtc && !occupied.Contains(date);
        bool RealTurn(Customer c, DateOnly week) => real.Any(r => r.VehicleId == vehicle && r.ZoneId == c.ZoneId
            && r.Days.Intersect(c.VisitDays!).Any() && BusinessTime.WeekStart(r.OpenedAt) == week);

        var requests = new List<(Guid Zone, DateOnly Date)>();
        var seededSales = new DateOnly?[3];
        var realSales = new DateTime?[3];
        for (var i = 0; i < 3; i++)
        {
            var customer = customers[i];
            // A real purchase that already gives the target needs no seeded sale.
            if (purchases.TryGetValue(customer.Id, out var bought)
                && CustomerActivityPolicy.MissedWeeks(customer, bought, real) == targets[i])
            {
                realSales[i] = bought;
                continue;
            }
            // Walk the sale back week by week. Every later week counts one turn: a real
            // received trip if there is one, otherwise a seeded trip on a free visit day.
            for (var back = 0; back <= 26 && seededSales[i] is null; back++)
            {
                var fills = new List<DateOnly>();
                var turns = 0;
                for (var k = back - 1; k >= 0; k--)
                {
                    var week = monday.AddDays(-7 * k);
                    if (RealTurn(customer, week)) { turns++; continue; }
                    var free = customer.VisitDays!.Order().Select(d => week.AddDays(d - 1)).Where(Free).ToArray();
                    if (free.Length > 0) { fills.Add(free[0]); turns++; }
                }
                if (turns > targets[i]) break;
                if (turns < targets[i]) continue;
                var saleWeek = monday.AddDays(-7 * back);
                var saleDays = customer.VisitDays!.OrderDescending().Select(d => saleWeek.AddDays(d - 1)).Where(Free).ToArray();
                if (saleDays.Length == 0) continue;
                seededSales[i] = saleDays[0];
                requests.Add((customer.ZoneId, saleDays[0]));
                requests.AddRange(fills.Select(date => (customer.ZoneId, date)));
            }
            if (seededSales[i] is null)
                throw new InvalidOperationException($"No hay fechas libres para dejar a {customer.ContactName} en {targets[i]} turnos perdidos. Elegí otro cliente.");
        }
        // Up to three non-overlapping zone trips per day (08-10, 11-13, 14-16).
        var trips = requests.Distinct().GroupBy(r => r.Date).OrderBy(g => g.Key)
            .SelectMany(g => g.OrderBy(r => r.Zone).Select((r, index) => new PlannedTrip(
                r.Zone, Utc(r.Date, 8 + index * 3), Utc(r.Date, 10 + index * 3),
                ((int)r.Date.DayOfWeek + 6) % 7 + 1))).ToArray();
        var people = customers.Select((c, i) => seededSales[i] is { } date
            ? new PlannedCustomer(c, trips.Single(t => t.ZoneId == c.ZoneId
                && DateOnly.FromDateTime(t.OpenedAt.Add(BusinessTime.Offset)) == date).OpenedAt.AddHours(1), targets[i])
            : new PlannedCustomer(c, realSales[i]!.Value, targets[i], Seeded: false)).ToArray();
        // Seeded trips of one customer may add turns to another: verify the whole plan.
        var departures = real.Concat(trips.Select(t => new RouteDeparture(vehicle, t.ZoneId, [t.Day], t.OpenedAt))).ToArray();
        if (people.Any(p => CustomerActivityPolicy.MissedWeeks(p.Customer, p.SaleAt, departures) != p.Expected))
            throw new InvalidOperationException("Días/zonas incompatibles con 0/3/5: las salidas de un cliente suman turnos a otro. Elegí otros clientes.");
        return new(people, trips);
    }

    // Fit historical trips backwards between real odometer readings; never rewrite the vehicle.
    public static Dictionary<DateTime, (int Open, int Close)> Kilometers(
        PlannedTrip[] trips, IEnumerable<DeliverySession> realSessions, int current)
    {
        var real = realSessions.OrderBy(s => s.OpenedAt).ToArray();
        var result = new Dictionary<DateTime, (int, int)>();
        var upper = current;
        foreach (var trip in trips.OrderByDescending(t => t.OpenedAt))
        {
            var next = real.FirstOrDefault(s => s.OpenedAt > trip.OpenedAt);
            if (next is not null) upper = Math.Min(upper, next.KilometersAtOpen);
            var previous = real.LastOrDefault(s => s.OpenedAt < trip.OpenedAt);
            var lower = previous?.KilometersAtClose ?? previous?.KilometersAtOpen ?? 0;
            if (upper - 1 < lower)
                throw new InvalidOperationException($"Sin espacio de kilometraje histórico para {trip.OpenedAt:O}. Elegí otros clientes/fechas; no se modifica el odómetro real.");
            result[trip.OpenedAt] = (upper - 1, upper);
            upper--;
        }
        return result;
    }
}
