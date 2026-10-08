using System.Globalization;
using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Data.Context;
using GMSoft.Data.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActivitySimulation;

public static class FirstVisit
{
    public const string Prefix = "[GMSoft.ActivitySimulation/first-visit/v1]";

    public static ActivityPlan Plan(FirstVisitOptions options, Guid vehicle, Zone zone, int nextRouteOrder,
        Guid run, IEnumerable<RouteDeparture> departures)
    {
        var opened = Planner.Utc(options.Date, 8);
        var saleAt = opened.AddHours(1);
        var day = BusinessTime.IsoDayOfWeek(opened);
        var dayName = CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetDayName(options.Date.DayOfWeek);
        dayName = char.ToUpperInvariant(dayName[0]) + dayName[1..];
        var people = Enumerable.Range(0, options.Count).Select(i => new Customer
        {
            Id = Guid.NewGuid(), ContactName = $"Cliente {dayName} {i + 1} (Simulación)",
            Address = $"Calle Ficticia {100 + i} (Simulación)", Phone = $"00000000{i + 1:00}",
            VehicleId = vehicle, ZoneId = zone.Id, Zone = zone, VisitDays = [day], IsActive = true,
            RouteOrder = checked(nextRouteOrder + i), CreatedAt = saleAt, UpdatedAt = saleAt,
            LastVisitAt = saleAt, Notes = $"{Prefix} {run} Cliente ficticio; primera visita."
        }).ToArray();
        var real = departures.ToArray();
        return new(people.Select(c => new PlannedCustomer(c, saleAt,
                CustomerActivityPolicy.MissedWeeks(c, saleAt, real))).ToArray(),
            [new(zone.Id, opened, opened.AddHours(2), day)]);
    }

    public static Dictionary<Guid, int> Quantities(ActivityPlan plan) => plan.Customers
        .Select((p, i) => (p.Customer.Id, Quantity: 1 + i % 2)).ToDictionary(p => p.Id, p => p.Quantity);

    public static async Task<int> Run(AppDbContext db, CustomerActivityPolicy policy, FirstVisitOptions options,
        Driver driver, Vehicle vehicle, string database, string path)
    {
        // Match street registration: order is at the end of the whole zone, across vehicles.
        var zoneIds = await db.Customers.Where(c => c.VehicleId == vehicle.Id)
            .Select(c => c.ZoneId).Distinct().ToArrayAsync();
        var zones = await db.Zones.Where(z => z.IsActive && zoneIds.Contains(z.Id)).ToListAsync();
        Zone zone;
        if (options.Zone is null)
        {
            if (zoneIds.Length != 1 || zones.Count != 1)
                throw new InvalidOperationException("No hay una única zona activa entre los clientes del vehículo. Indicá --zone ID_o_nombre. Zonas: "
                    + string.Join(" / ", zones.Select(z => $"{z.Name} [{z.Id}]")));
            zone = zones[0];
        }
        else
        {
            var matches = zones.Where(z => Guid.TryParse(options.Zone, out var id) ? z.Id == id
                : string.Equals(z.Name, options.Zone, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("--zone debe identificar una única zona activa de los clientes actuales del vehículo; usá su ID.");
            zone = matches[0];
        }
        var repository = new CustomerRepository(db);
        var run = Guid.NewGuid();
        var departures = await repository.GetClosedDeparturesAsync([vehicle.Id], Planner.Utc(options.Date, 0));
        var plan = Plan(options, vehicle.Id, zone, await repository.GetNextRouteOrderAsync(zone.Id), run, departures);
        var sessions = await db.DeliverySessions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.VehicleId == vehicle.Id || (s.DriverId == driver.Id && s.Status == SessionStatus.Open)).ToListAsync();
        var conflicts = new List<string>();
        var (from, to) = BusinessTime.DayRangeUtc(options.Date);
        foreach (var session in sessions)
        {
            if (session.Status == SessionStatus.Open) conflicts.Add($"Salida abierta {session.Id}.");
            if (session.VehicleId == vehicle.Id && session.OpenedAt < to && (session.ClosedAt ?? session.OpenedAt) >= from)
                conflicts.Add($"Salida existente {session.Id} ocupa {options.Date:yyyy-MM-dd}.");
        }
        var names = plan.Customers.Select(p => p.Customer.ContactName.ToLowerInvariant()).ToArray();
        var duplicates = await db.Customers.IgnoreQueryFilters().Where(c => names.Contains(c.ContactName.ToLower())
            || (c.BusinessName != null && names.Contains(c.BusinessName.ToLower())))
            .Select(c => new { c.Id, c.ContactName }).ToListAsync();
        conflicts.AddRange(duplicates.Select(c => $"Nombre de cliente ya existente: {c.ContactName} [{c.Id}]."));
        if (conflicts.Count > 0)
            throw new InvalidOperationException("Conflictos; no se escribe nada:\n- " + string.Join("\n- ", conflicts));
        var kilometers = Planner.Kilometers(plan.Trips, sessions.Where(s => s.VehicleId == vehicle.Id), vehicle.CurrentKilometers);
        var product = await db.Products.Where(p => p.IsPublished && p.Tracking == ContainerTracking.ByBalance)
            .OrderBy(p => p.Id).FirstOrDefaultAsync()
            ?? await db.Products.Where(p => p.IsPublished && p.Tracking == ContainerTracking.None)
                .OrderBy(p => p.Id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No existe producto publicado ByBalance ni None.");
        if (product.SalePrice <= 0)
            throw new InvalidOperationException("El producto publicado seleccionado no tiene precio de venta positivo.");
        var admin = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where u.IsActive && r.NormalizedName == "ADMIN" orderby u.Id select (Guid?)u.Id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No hay usuario Admin activo para carga/recepción/liquidación.");
        var quantities = Quantities(plan);
        var prices = plan.Customers.ToDictionary(p => p.Customer.Id, _ => product.SalePrice);
        Console.WriteLine($"{options.Mode} | first-visit | reparto1 | vehículo {vehicle.Name} [{vehicle.Id}] | zona {zone.Name} [{zone.Id}]");
        var trip = plan.Trips.Single();
        var km = kilometers[trip.OpenedAt];
        Console.WriteLine($"Salida {options.Date:yyyy-MM-dd} 08:00–10:00 AR | día ISO {trip.Day} | km {km.Open} → {km.Close}; odómetro sin cambios.");
        Console.WriteLine($"Producto {product.Detail} [{product.Id}] | {product.Tracking} | precio {product.SalePrice}");
        foreach (var p in plan.Customers)
            Console.WriteLine($"{p.Customer.ContactName} | orden {p.Customer.RouteOrder} | venta {p.SaleAt.Add(BusinessTime.Offset):yyyy-MM-dd HH:mm} AR | unidades {quantities[p.Customer.Id]} | efectivo {prices[p.Customer.Id] * quantities[p.Customer.Id]} | envases retenidos {(product.Tracking == ContainerTracking.ByBalance ? quantities[p.Customer.Id] : 0)} | esperado {policy.GetStatus(p.Expected)} ({p.Expected} turnos perdidos)");
        Console.WriteLine("Las salidas también pueden afectar la actividad calculada de otros clientes del vehículo/zona/día.");
        // Build exactly the rows being proposed even in dry-run; tracking entities is not a database write.
        foreach (var p in plan.Customers) db.Customers.Add(p.Customer);
        var rows = SeedBuilder.Build(db, plan, driver, admin, product, prices, kilometers, run, quantities);
        rows.AddRange(plan.Customers.Select(p => p.Customer));
        if (options.Mode == "--dry-run")
        {
            Console.WriteLine($"Plan válido: {rows.Count} filas nuevas. Dry-run: cero escrituras en base y cero archivos creados; lectura de clientes nuevos pendiente de apply.");
            return 0;
        }
        // Preserve the historical timestamps; the async override overwrites them with UtcNow.
        db.SaveChanges();
        var ids = plan.Customers.Select(p => p.Customer.Id).ToArray();
        var saved = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        await PrintActivity(db, policy, saved, plan.Customers);
        var snapshots = new List<SeedRow>();
        foreach (var row in rows)
        {
            var table = db.Model.FindEntityType(row.GetType())!.GetTableName()!;
            snapshots.Add(new(table, row.Id, (await Manifest.Snapshot(db, table, row.Id))!));
        }
        new Manifest(1, database, run, ids, snapshots, []).Write(path);
        await db.Database.CurrentTransaction!.CommitAsync();
        Console.WriteLine("Apply first-visit confirmado en una transacción. Conservar tools/ActivitySimulation/first-visit-manifest.json para undo.");
        return 0;
    }

    public static async Task PrintActivity(AppDbContext db, CustomerActivityPolicy policy, List<Customer> people,
        PlannedCustomer[]? expected = null)
    {
        var repository = new CustomerRepository(db);
        var purchases = await repository.GetLastPurchaseDatesAsync(people.Select(c => c.Id).ToArray());
        var missed = await CustomerActivityReader.MissedWeeksAsync(repository, people, purchases, default);
        foreach (var c in people)
        {
            var purchase = purchases.TryGetValue(c.Id, out var at) ? (DateTime?)at : null;
            Console.WriteLine($"Lectura real CustomerActivityReader: {c.ContactName} [{c.Id}] | lastPurchase {purchase?.Add(BusinessTime.Offset):yyyy-MM-dd HH:mm} AR | {policy.GetStatus(missed[c.Id])} ({missed[c.Id]} turnos perdidos)");
            var wanted = expected?.Single(p => p.Customer.Id == c.Id);
            if (wanted is not null && (purchase != wanted.SaleAt || c.LastVisitAt != wanted.SaleAt || missed[c.Id] != wanted.Expected))
                throw new InvalidOperationException("La lectura real de primera visita no coincide con el plan; transacción revertida.");
        }
    }
}
