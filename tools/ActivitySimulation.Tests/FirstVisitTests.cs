using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActivitySimulation.Tests;

public class FirstVisitTests
{
    private static readonly DateTime Thursday = new(2026, 10, 8, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DefaultScenarioAndExplicitActivityPreserveLegacyArguments()
    {
        Assert.Equal("activity", ScenarioOptions.Parse([]).Scenario);
        var result = ScenarioOptions.Parse(["--apply", "--scenario", "activity", "--a", "Cliente A"]);
        Assert.Equal("activity", result.Scenario);
        Assert.Equal(new[] { "--apply", "--a", "Cliente A" }, result.Arguments);
        Assert.Equal("first-visit", ScenarioOptions.Parse(["--scenario", "first-visit"]).Scenario);
    }

    [Theory]
    [InlineData("--scenario")]
    [InlineData("--scenario", "unknown")]
    [InlineData("--scenario", "activity", "--scenario", "first-visit")]
    public void RejectsInvalidScenario(params string[] args) =>
        Assert.Throws<InvalidOperationException>(() => ScenarioOptions.Parse(args));

    [Fact]
    public void DefaultsToPreviousThursdayAndThreeCustomers()
    {
        var options = FirstVisitOptions.Parse([], Thursday);
        Assert.Equal(new DateOnly(2026, 10, 1), options.Date);
        Assert.Equal(3, options.Count);
        Assert.Null(options.Zone);
        Assert.Equal("--dry-run", options.Mode);
    }

    [Theory]
    [InlineData(2026, 10, 8, 1, 2026, 9, 30, 3)] // UTC Thursday is still Wednesday in Argentina.
    [InlineData(2026, 1, 1, 18, 2025, 12, 25, 4)]
    [InlineData(2028, 3, 5, 18, 2028, 2, 27, 7)]
    [InlineData(2026, 3, 2, 18, 2026, 2, 23, 1)]
    public void DefaultDateAndIsoDayRespectArgentinaAndCalendarBoundaries(
        int year, int month, int day, int hour, int expectedYear, int expectedMonth, int expectedDay, int iso)
    {
        var options = FirstVisitOptions.Parse([], new(year, month, day, hour, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), options.Date);
        var plan = FirstVisit.Plan(options, Guid.NewGuid(), new Zone { Id = Guid.NewGuid() }, 1, Guid.NewGuid(), []);
        Assert.Equal(iso, plan.Trips.Single().Day);
        Assert.All(plan.Customers, p => Assert.Equal(new[] { iso }, p.Customer.VisitDays));
    }

    [Fact]
    public void ExplicitDateCountAndZoneAreAccepted()
    {
        var options = FirstVisitOptions.Parse(["--date", "2026-09-27", "--count", "10", "--zone", "Centro", "--apply"], Thursday);
        Assert.Equal(new DateOnly(2026, 9, 27), options.Date);
        Assert.Equal(10, options.Count);
        Assert.Equal("Centro", options.Zone);
        Assert.Equal("--apply", options.Mode);
        Assert.Equal(1, FirstVisitOptions.Parse(["--count", "1"], Thursday).Count);
        Assert.Equal("--undo", FirstVisitOptions.Parse(["--undo"], Thursday).Mode);
    }

    [Theory]
    [InlineData("--date", "2026-10-08")]
    [InlineData("--date", "2026-10-09")]
    [InlineData("--date", "2026-02-30")]
    [InlineData("--date", "2026-9-1")]
    [InlineData("--date", "01/09/2026")]
    [InlineData("--date")]
    [InlineData("--count", "0")]
    [InlineData("--count", "11")]
    [InlineData("--count", "1.5")]
    [InlineData("--count", "-1")]
    [InlineData("--count", "three")]
    [InlineData("--count", "3", "--count", "4")]
    [InlineData("--zone")]
    [InlineData("--zone", " ")]
    [InlineData("--zone", "--apply")]
    [InlineData("--date", "2026-09-01", "--date", "2026-09-02")]
    [InlineData("--apply", "--dry-run")]
    [InlineData("--apply", "--apply")]
    [InlineData("--undo", "--count", "3")]
    [InlineData("--undo", "--zone", "Centro")]
    [InlineData("--undo", "--date", "2026-09-01")]
    [InlineData("--a", "Cliente A")]
    [InlineData("--unknown")]
    public void RejectsInvalidArguments(params string[] args) =>
        Assert.Throws<InvalidOperationException>(() => FirstVisitOptions.Parse(args, Thursday));

    [Fact]
    public void ExplicitDateCannotBeTodayInArgentinaEvenIfUtcDateIsTomorrow()
    {
        var now = new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc);
        Assert.Throws<InvalidOperationException>(() => FirstVisitOptions.Parse(["--date", "2026-10-07"], now));
        Assert.Equal(new DateOnly(2026, 10, 6), FirstVisitOptions.Parse(["--date", "2026-10-06"], now).Date);
    }

    [Fact]
    public void NewCustomersMatchStreetRegistrationAndAppendInOrder()
    {
        var vehicle = Guid.NewGuid();
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Centro" };
        var plan = FirstVisit.Plan(FirstVisitOptions.Parse([], Thursday), vehicle, zone, 42, Guid.NewGuid(), []);
        Assert.Equal(new[] { 42, 43, 44 }, plan.Customers.Select(p => p.Customer.RouteOrder));
        Assert.Equal(3, plan.Customers.Select(p => p.Customer.ContactName).Distinct().Count());
        Assert.All(plan.Customers, p =>
        {
            var c = p.Customer;
            Assert.Equal(vehicle, c.VehicleId);
            Assert.Equal(zone.Id, c.ZoneId);
            Assert.True(c.IsActive);
            Assert.Null(c.BusinessName);
            Assert.Equal(p.SaleAt, c.CreatedAt);
            Assert.Equal(p.SaleAt, c.UpdatedAt);
            Assert.Equal(p.SaleAt, c.LastVisitAt);
            Assert.Equal(new DateOnly(2026, 10, 1), DateOnly.FromDateTime(c.CreatedAt.Add(BusinessTime.Offset)));
            Assert.Equal(DateTimeKind.Utc, c.CreatedAt.Kind);
            Assert.StartsWith(FirstVisit.Prefix, c.Notes);
            Assert.True(new NewCustomerLineValidator().Validate(new NewCustomerLine(c.BusinessName, c.ContactName,
                c.Phone, c.Address, c.Notes, c.VisitDays)).IsValid);
            Assert.Equal(0, p.Expected);
            Assert.Equal("White", new CustomerActivityPolicy(2, 4).GetStatus(p.Expected));
        });
    }

    [Fact]
    public void LaterReceivedDeparturesReportRealMissedWeeksInsteadOfRequiringZero()
    {
        var vehicle = Guid.NewGuid();
        var zone = new Zone { Id = Guid.NewGuid() };
        RouteDeparture[] real = [new(vehicle, zone.Id, [4], Planner.Utc(new(2026, 10, 8), 8)),
            new(vehicle, zone.Id, [4], Planner.Utc(new(2026, 10, 9), 8)),
            new(vehicle, zone.Id, [4], Planner.Utc(new(2026, 10, 2), 8)), // Sale week does not count.
            new(Guid.NewGuid(), zone.Id, [4], Planner.Utc(new(2026, 10, 8), 8)),
            new(vehicle, Guid.NewGuid(), [4], Planner.Utc(new(2026, 10, 8), 8)),
            new(vehicle, zone.Id, [3], Planner.Utc(new(2026, 10, 8), 8))];
        var plan = FirstVisit.Plan(FirstVisitOptions.Parse([], Thursday), vehicle, zone, 1, Guid.NewGuid(), real);
        Assert.All(plan.Customers, p => Assert.Equal(1, p.Expected));
    }

    [Theory]
    [InlineData(ContainerTracking.ByBalance, 3)]
    [InlineData(ContainerTracking.None, 3)]
    [InlineData(ContainerTracking.ByBalance, 1)]
    [InlineData(ContainerTracking.ByBalance, 10)]
    public void FirstSaleReconcilesCashStockAndRetainedContainers(ContainerTracking tracking, int count)
    {
        var vehicle = Guid.NewGuid();
        var plan = FirstVisit.Plan(FirstVisitOptions.Parse(["--count", count.ToString()], Thursday), vehicle,
            new Zone { Id = Guid.NewGuid() }, 1, Guid.NewGuid(), []);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var product = new Product { Id = Guid.NewGuid(), Tracking = tracking, SalePrice = 12.34m };
        var quantities = FirstVisit.Quantities(plan);
        var rows = SeedBuilder.Build(db, plan, new Driver { Id = Guid.NewGuid(), VehicleId = vehicle,
            ApplicationUserId = Guid.NewGuid() }, Guid.NewGuid(), product,
            plan.Customers.ToDictionary(p => p.Customer.Id, _ => product.SalePrice),
            Planner.Kilometers(plan.Trips, [], 100), Guid.NewGuid(), quantities);
        Assert.Equal(count, rows.OfType<Delivery>().Count());
        Assert.Equal(Enumerable.Range(0, count).Select(i => 1 + i % 2), plan.Customers.Select(p => quantities[p.Customer.Id]));
        Assert.All(rows, r => { Assert.NotEqual(Guid.Empty, r.Id); Assert.Equal(new DateOnly(2026, 10, 1), DateOnly.FromDateTime(r.CreatedAt.Add(BusinessTime.Offset))); });
        Assert.All(rows.OfType<SessionStockMovement>().GroupBy(m => (m.ProductId, m.State)), g => Assert.Equal(0, g.Sum(m => m.Quantity)));
        Assert.DoesNotContain(rows.OfType<SessionStockMovement>(), m => m.State == ContainerState.Empty);
        var load = Assert.Single(rows.OfType<VehicleLoad>());
        Assert.Equal(quantities.Values.Sum() + 1, load.Quantity);
        var session = Assert.Single(rows.OfType<DeliverySession>());
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(session.Id, load.ConsumedBySessionId);
        Assert.True(load.LoadedAt < session.OpenedAt);
        Assert.Equal(session.ClosedAt, session.UpdatedAt);
        var stock = rows.OfType<SessionStockMovement>().OrderBy(m => m.OccurredAt).ToArray();
        var balance = 0;
        foreach (var movement in stock) { balance += movement.Quantity; Assert.True(balance >= 0); }
        Assert.Equal(0, balance);
        var received = Assert.Single(stock, m => m.Type == SessionStockMovementType.ReturnedAtClose);
        Assert.Equal(-1, received.Quantity);
        foreach (var sale in rows.OfType<Delivery>())
        {
            var quantity = quantities[sale.CustomerId];
            var item = rows.OfType<DeliveryItem>().Single(i => i.DeliveryId == sale.Id);
            Assert.Equal(quantity, item.Quantity);
            Assert.Equal(product.SalePrice, item.UnitPrice);
            Assert.Equal(quantity * product.SalePrice, sale.Total);
            var payment = rows.OfType<Payment>().Single(p => p.CustomerId == sale.CustomerId);
            Assert.Equal(sale.Total, payment.Amount);
            Assert.Equal(PaymentMethod.Cash, payment.Method);
            Assert.Equal(sale.DeliveredAt, payment.PaidAt);
            if (tracking == ContainerTracking.ByBalance)
            {
                var movement = rows.OfType<ContainerMovement>().Single(m => m.CustomerId == sale.CustomerId);
                Assert.Equal(ContainerMovementType.DeliveredToCustomer, movement.Type);
                Assert.Equal(quantity, movement.Quantity);
                Assert.Equal(sale.Id, movement.DeliveryId);
                Assert.Equal(quantity, rows.OfType<CustomerContainerBalance>().Single(b => b.CustomerId == sale.CustomerId).Quantity);
            }
        }
        if (tracking == ContainerTracking.None)
        {
            Assert.Empty(rows.OfType<ContainerMovement>());
            Assert.Empty(rows.OfType<CustomerContainerBalance>());
        }
        Assert.Equal(rows.OfType<Payment>().Sum(p => p.Amount), rows.OfType<Delivery>().Sum(d => d.Total));
        Assert.Equal(rows.OfType<Payment>().Sum(p => p.Amount), Assert.Single(rows.OfType<SessionCashSettlement>()).AmountReceived);
        Assert.All(rows.OfType<Payment>(), p => Assert.StartsWith(FirstVisit.Prefix, p.Notes));
    }
}
