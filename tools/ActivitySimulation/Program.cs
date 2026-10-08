using System.Data;
using System.Security.Cryptography;
using System.Text;
using ActivitySimulation;
using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Data.Context;
using GMSoft.Data.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

try { return await Run(args); }
catch (InvalidOperationException ex) { Console.Error.WriteLine(ex.Message); return 1; }
catch (Exception ex)
{
    // Provider/config exceptions can contain secrets or row data. Never dump them.
    Console.Error.WriteLine($"Operación abortada ({ex.GetType().Name}). Revisá conexión local, permisos y esquema. No se imprimen detalles sensibles.");
    return 1;
}

static async Task<int> Run(string[] args)
{
    var mode = "--dry-run";
    var explicitMode = false;
    var selectors = new string?[3];
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--dry-run" or "--apply" or "--undo")
        {
            if (explicitMode) throw new InvalidOperationException("Indicá un solo modo: --dry-run, --apply o --undo.");
            mode = args[i]; explicitMode = true;
        }
        else if (args[i] is "--a" or "--b" or "--c")
        {
            var index = args[i][2] - 'a';
            if (++i >= args.Length || selectors[index] is not null)
                throw new InvalidOperationException("Cada --a/--b/--c requiere un ID o nombre exacto único.");
            selectors[index] = args[i];
        }
        else throw new InvalidOperationException("Uso: [--dry-run|--apply|--undo] [--a ID_o_nombre] [--b ID_o_nombre] [--c ID_o_nombre]");
    }
    if (mode == "--undo" && selectors.Any(s => s is not null))
        throw new InvalidOperationException("--undo usa exclusivamente los IDs del manifiesto, sin selectores.");
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GMSoft.slnx"))) directory = directory.Parent;
    var root = directory?.FullName ?? throw new InvalidOperationException("Ejecutá desde el checkout de GM-SoftBE.");
    var (config, connection) = Settings.Load(root);
    var policy = new CustomerActivityPolicy(config.GetValue<int>("CustomerActivity:RedAfterMissedWeeks"),
        config.GetValue<int>("CustomerActivity:BlackAfterMissedWeeks"));
    if (policy.RedAfterMissedWeeks != 2 || policy.BlackAfterMissedWeeks != 4)
        throw new InvalidOperationException("Este escenario requiere los umbrales vigentes 2/4; revisá overrides del entorno. No se cambió configuración.");
    var database = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{connection.Host!.ToLowerInvariant()}:{connection.Port}/{connection.Database}/{connection.Username}")));
    var folder = Path.Combine(root, "tools", "ActivitySimulation");
    var path = Path.Combine(folder, "activity-manifest.json");
    // Dry-run creates no files. Writers also serialize across processes/checkouts in PostgreSQL.
    using var fileLock = mode == "--dry-run" ? null : new FileStream(Path.Combine(folder, "activity.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connection.ConnectionString).Options);
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
    if (mode == "--dry-run") await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
    else
    {
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'");
        // Short-lived maintenance lock: prevents API writes between conflict checks and commit.
        var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()!).Distinct().Order().Select(Manifest.Quote);
#pragma warning disable EF1003 // Identifiers come exclusively from the compiled EF model and are quoted.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE " + string.Join(", ", tables) + " IN SHARE ROW EXCLUSIVE MODE");
#pragma warning restore EF1003
    }
    if (File.Exists(path))
    {
        var existing = Manifest.Read(path, database);
        var present = await existing.Verify(db);
        if (mode == "--undo")
        {
            if (present) await existing.Undo(db);
            await transaction.CommitAsync();
            File.Delete(path);
            Console.WriteLine(present ? "Undo completo: solo filas del manifiesto; LastVisitAt restaurado." : "No hay filas del manifiesto en la base; se retiró el manifiesto pendiente.");
            return 0;
        }
        if (!present) throw new InvalidOperationException("Manifiesto pendiente sin filas. Ejecutá --undo para retirarlo antes de --apply.");
        Console.WriteLine("Simulación ya sembrada; no se duplica. --undo permite preparar otra selección.");
        var people = await db.Customers.Where(c => existing.Customers.Contains(c.Id)).ToListAsync();
        await PrintActivity(db, policy, people);
        return 0;
    }
    var marker = SeedBuilder.Prefix;
    if (await db.SessionCashSettlements.IgnoreQueryFilters().AnyAsync(s => s.Notes != null && s.Notes.StartsWith(marker)))
        throw new InvalidOperationException("Hay una simulación en la base sin su manifiesto local. Recuperá activity-manifest.json; no es seguro duplicar ni borrar por prefijo.");
    if (mode == "--undo") { Console.WriteLine("No hay simulación ni manifiesto para deshacer."); return 0; }

    var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == "REPARTO1" && u.IsActive)
        ?? throw new InvalidOperationException("No existe la cuenta activa reparto1.");
    var driver = await db.Drivers.SingleOrDefaultAsync(d => d.ApplicationUserId == user.Id && d.IsActive)
        ?? throw new InvalidOperationException("reparto1 no tiene chofer activo.");
    var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == driver.VehicleId)
        ?? throw new InvalidOperationException("reparto1 no tiene vehículo asignado disponible.");
    var eligible = (await db.Customers.Include(c => c.Zone).Where(c => c.IsActive && c.VehicleId == vehicle.Id
        && c.VisitDays != null && c.VisitDays.Length > 0 && c.Zone.IsActive).ToListAsync())
        .OrderBy(c => c.RouteOrder).ThenBy(c => c.Id).ToArray();
    if (eligible.Length < 3) throw new InvalidOperationException($"Solo hay {eligible.Length} clientes activos con días de visita y zona activa para reparto1; se requieren tres.");
    var selected = new Customer?[3];
    for (var i = 0; i < 3; i++)
    {
        if (selectors[i] is not { } selector) continue;
        var matches = eligible.Where(c => Guid.TryParse(selector, out var id) ? c.Id == id
            : string.Equals(c.ContactName, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.BusinessName, selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"Selector '{selector}': {matches.Length} coincidencias elegibles. Usá un ID inequívoco.");
        selected[i] = matches[0];
    }
    for (var i = 0; i < 3; i++) selected[i] ??= eligible.First(c => !selected.Contains(c));
    for (var i = 0; i < 3; i++)
        Console.WriteLine($"Cliente {(char)('A' + i)}: {selected[i]!.BusinessName ?? selected[i]!.ContactName} [{selected[i]!.Id}]");
    var now = DateTime.UtcNow;
    var sessions = await db.DeliverySessions.IgnoreQueryFilters().AsNoTracking()
        .Where(s => s.VehicleId == vehicle.Id || (s.DriverId == driver.Id && s.Status == SessionStatus.Open)).ToListAsync();
    var repository = new CustomerRepository(db);
    // The plan is built around real history: received trips count as turns and their days stay untouched.
    var realDepartures = await repository.GetClosedDeparturesAsync([vehicle.Id], now.AddDays(-7 * 30));
    var realPurchases = await repository.GetLastPurchaseDatesAsync(selected.Select(c => c!.Id).ToArray());
    var occupied = sessions.Where(s => s.VehicleId == vehicle.Id).SelectMany(s => new[] { s.OpenedAt, s.ClosedAt ?? s.OpenedAt })
        .Select(at => DateOnly.FromDateTime(at.Add(BusinessTime.Offset))).ToHashSet();
    var plan = Planner.Create(now, selected.Select(c => c!).ToArray(), realDepartures, realPurchases, occupied);
    Console.WriteLine($"{mode} | reparto1 | vehículo {vehicle.Name} ({vehicle.Id}) | hora argentina UTC-03");
    foreach (var p in plan.Customers)
        Console.WriteLine($"{p.Customer.BusinessName ?? p.Customer.ContactName} [{p.Customer.Id}] zona={p.Customer.Zone.Name} días={string.Join(',', p.Customer.VisitDays!)}: {(p.Seeded ? "venta sembrada" : "venta real existente, no se siembra nada")} {p.SaleAt.Add(BusinessTime.Offset):yyyy-MM-dd HH:mm}; esperado {policy.GetStatus(p.Expected)} ({p.Expected})");
    foreach (var t in plan.Trips)
        Console.WriteLine($"Salida {t.OpenedAt.Add(BusinessTime.Offset):yyyy-MM-dd HH:mm}–{t.ClosedAt.Add(BusinessTime.Offset):HH:mm} AR | UTC {t.OpenedAt:O}–{t.ClosedAt:O} | zona {t.ZoneId} | ventas {string.Join(", ", plan.Customers.Where(p => p.Seeded && p.Customer.ZoneId == t.ZoneId && p.SaleAt == t.OpenedAt.AddHours(1)).Select(p => p.Customer.ContactName))}");

    var conflicts = new List<string>();
    foreach (var session in sessions)
    {
        if (session.Status == SessionStatus.Open) conflicts.Add($"Salida abierta {session.Id}.");
        foreach (var date in plan.Trips.Select(t => DateOnly.FromDateTime(t.OpenedAt.Add(BusinessTime.Offset))).Distinct())
        {
            var (from, to) = BusinessTime.DayRangeUtc(date);
            if (session.OpenedAt < to && (session.ClosedAt ?? session.OpenedAt) >= from)
                conflicts.Add($"Salida real {session.Id} ocupa {date:yyyy-MM-dd}.");
        }
    }
    var departures = await repository.GetClosedDeparturesAsync([vehicle.Id], plan.Customers.Min(p => p.SaleAt));
    var plannedDepartures = plan.Trips.Select(t => new RouteDeparture(vehicle.Id, t.ZoneId, [t.Day], t.OpenedAt));
    foreach (var p in plan.Customers)
    {
        var later = await db.Deliveries.IgnoreQueryFilters().Where(d => d.CustomerId == p.Customer.Id
            && d.Type == DeliveryType.Sale && d.DeliveredAt > p.SaleAt).Select(d => new { d.Id, d.DeliveredAt }).ToListAsync();
        conflicts.AddRange(later.Select(d => $"{p.Customer.ContactName}: venta real posterior {d.Id}, {d.DeliveredAt:O}."));
        var actual = CustomerActivityPolicy.MissedWeeks(p.Customer, p.SaleAt, departures.Concat(plannedDepartures));
        if (actual != p.Expected) conflicts.Add($"{p.Customer.ContactName}: las salidas existentes producirían {actual} turnos, se esperaban {p.Expected}.");
    }
    if (conflicts.Count > 0)
        throw new InvalidOperationException("Conflictos; no se escribe nada:\n- " + string.Join("\n- ", conflicts.Distinct())
            + "\nElegí otros clientes con --a/--b/--c. Una salida abierta debe resolverse por el circuito habitual de la app.");
    var kilometers = Planner.Kilometers(plan.Trips, sessions.Where(s => s.VehicleId == vehicle.Id), vehicle.CurrentKilometers);
    var product = await db.Products.Where(p => p.IsPublished && p.Tracking != ContainerTracking.ByUnit && p.SalePrice > 0)
        .OrderBy(p => p.Id).FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("No existe producto publicado con precio positivo y seguimiento None/ByBalance.");
    var prices = new Dictionary<Guid, decimal>();
    foreach (var p in plan.Customers)
    {
        var price = await db.CustomerProductPrices.Where(x => x.CustomerId == p.Customer.Id && x.ProductId == product.Id)
            .Select(x => (decimal?)x.Price).SingleOrDefaultAsync() ?? product.SalePrice;
        if (price <= 0) throw new InvalidOperationException($"Precio no positivo para {p.Customer.ContactName}; no se puede simular cobro completo.");
        prices.Add(p.Customer.Id, price);
    }
    var admin = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
        join r in db.Roles on ur.RoleId equals r.Id
        where u.IsActive && r.NormalizedName == "ADMIN" orderby u.Id select (Guid?)u.Id).FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("No hay usuario Admin activo para carga/recepción/liquidación.");
    Console.WriteLine($"Producto: {product.Detail} [{product.Id}], {product.Tracking}. Una unidad por venta, efectivo: {string.Join(" / ", prices.Values)}.");
    foreach (var t in plan.Trips)
        Console.WriteLine($"{t.OpenedAt.Add(BusinessTime.Offset):yyyy-MM-dd HH:mm}: km {kilometers[t.OpenedAt].Open} → {kilometers[t.OpenedAt].Close}");
    Console.WriteLine("Las salidas también pueden afectar el estado calculado de otros clientes del mismo vehículo/zona/día.");
    if (mode == "--dry-run") { Console.WriteLine("Plan válido. Dry-run: cero escrituras en base y cero archivos creados."); return 0; }

    var run = Guid.NewGuid();
    var rows = SeedBuilder.Build(db, plan, driver, admin, product, prices, kilometers, run);
    // Deliberately use the inherited synchronous EF save: the production async override
    // replaces audit timestamps with UtcNow. All seed rows already have historical timestamps.
    db.SaveChanges();
    var changes = new List<CustomerChange>();
    foreach (var p in plan.Customers.Where(p => p.Seeded && (p.Customer.LastVisitAt is null || p.Customer.LastVisitAt < p.SaleAt)))
    {
        changes.Add(new(p.Customer.Id, p.Customer.LastVisitAt, p.SaleAt));
        await db.Customers.Where(c => c.Id == p.Customer.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.LastVisitAt, p.SaleAt));
    }
    var snapshots = new List<SeedRow>();
    foreach (var row in rows)
    {
        var table = db.Model.FindEntityType(row.GetType())!.GetTableName()!;
        snapshots.Add(new(table, row.Id, (await Manifest.Snapshot(db, table, row.Id))!));
    }
    await PrintActivity(db, policy, plan.Customers.Select(p => p.Customer).ToList(), plan.Customers);
    new Manifest(1, database, run, plan.Customers.Select(p => p.Customer.Id).ToArray(), snapshots, changes).Write(path);
    await transaction.CommitAsync();
    Console.WriteLine("Apply confirmado en una transacción. Manifiesto: tools/ActivitySimulation/activity-manifest.json (conservar para undo).");
    return 0;
}

static async Task PrintActivity(AppDbContext db, CustomerActivityPolicy policy, List<Customer> people,
    PlannedCustomer[]? expected = null)
{
    var repository = new CustomerRepository(db);
    var purchases = await repository.GetLastPurchaseDatesAsync(people.Select(c => c.Id).ToArray());
    var missed = await CustomerActivityReader.MissedWeeksAsync(repository, people, purchases, default);
    foreach (var c in people)
    {
        Console.WriteLine($"Lectura real CustomerActivityReader: {c.ContactName} [{c.Id}] = {policy.GetStatus(missed[c.Id])} ({missed[c.Id]} turnos perdidos)");
        if (expected is not null && missed[c.Id] != expected.Single(p => p.Customer.Id == c.Id).Expected)
            throw new InvalidOperationException("La comprobación en base no coincide con 0/3/5: transacción revertida.");
    }
}
