using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActivitySimulation.Tests;

public class PlannerTests
{
    private static Customer[] Customers(bool sharedZone = false, params int[][] days)
    {
        var vehicle = Guid.NewGuid();
        var zone = Guid.NewGuid();
        return Enumerable.Range(0, 3).Select(i => new Customer { Id = Guid.NewGuid(), VehicleId = vehicle,
            ZoneId = sharedZone ? zone : Guid.NewGuid(), VisitDays = days.Length == 0 ? [5] : days[i] }).ToArray();
    }

    private static void AssertPolicy(ActivityPlan plan)
    {
        var routes = plan.Trips.Select(t => new RouteDeparture(plan.Customers[0].Customer.VehicleId!.Value,
            t.ZoneId, [t.Day], t.OpenedAt)).ToArray();
        Assert.Equal(new[] { 0, 3, 5 }, plan.Customers.Select(p =>
            CustomerActivityPolicy.MissedWeeks(p.Customer, p.SaleAt, routes)));
        Assert.Equal(new[] { "White", "Red", "Black" }, plan.Customers.Select(p => new CustomerActivityPolicy(2, 4).GetStatus(p.Expected)));
        foreach (var p in plan.Customers)
            Assert.Contains(BusinessTime.IsoDayOfWeek(p.SaleAt), p.Customer.VisitDays!);
    }

    [Fact]
    public void BeforeVisit_UsesPreviousWeek_AndSharedTripsStillGiveZeroThreeFive()
    {
        var plan = Planner.Create(new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc), Customers(true));
        Assert.Equal(new DateOnly(2026, 10, 2), DateOnly.FromDateTime(plan.Customers[0].SaleAt.AddHours(-3)));
        Assert.Equal(new DateOnly(2026, 9, 11), DateOnly.FromDateTime(plan.Customers[1].SaleAt.AddHours(-3)));
        Assert.Equal(new DateOnly(2026, 8, 28), DateOnly.FromDateTime(plan.Customers[2].SaleAt.AddHours(-3)));
        Assert.Equal(6, plan.Trips.Length);
        AssertPolicy(plan);
    }

    [Fact]
    public void AfterVisit_UsesCurrentWeek_WhenRoutesDoNotInterfere()
    {
        var plan = Planner.Create(new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc), Customers());
        Assert.Equal(new DateOnly(2026, 10, 5), BusinessTime.WeekStart(plan.Customers[0].SaleAt));
        AssertPolicy(plan);
    }

    [Fact]
    public void CurrentSharedDeparture_CountsAsThisWeeksTurnForTheOthers()
    {
        var plan = Planner.Create(new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc), Customers(true));
        Assert.Equal(new DateOnly(2026, 10, 9), DateOnly.FromDateTime(plan.Customers[0].SaleAt.AddHours(-3)));
        Assert.Equal(new DateOnly(2026, 9, 18), DateOnly.FromDateTime(plan.Customers[1].SaleAt.AddHours(-3)));
        Assert.Equal(new DateOnly(2026, 9, 4), DateOnly.FromDateTime(plan.Customers[2].SaleAt.AddHours(-3)));
        AssertPolicy(plan);
    }

    [Fact]
    public void MultipleDays_StillGiveZeroThreeFive()
    {
        var plan = Planner.Create(new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc),
            Customers(true, [1, 5], [5], [5]));
        AssertPolicy(plan);
    }

    [Fact]
    public void RealHistory_ReusesRealSaleAndRealTrip_AndNeverSeedsOnAnOccupiedDay()
    {
        // Thursday: the three customers share Wednesday; a real trip was received yesterday
        // and A already bought in it.
        var now = new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);
        var customers = Customers(true, [3], [3], [3]);
        var realTrip = new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc);
        RouteDeparture[] real = [new(customers[0].VehicleId!.Value, customers[0].ZoneId, [3], realTrip)];
        var plan = Planner.Create(now, customers, real,
            new Dictionary<Guid, DateTime> { [customers[0].Id] = realTrip.AddMinutes(25) },
            new HashSet<DateOnly> { new(2026, 10, 7) });

        Assert.False(plan.Customers[0].Seeded);
        Assert.Equal(new DateOnly(2026, 9, 16), DateOnly.FromDateTime(plan.Customers[1].SaleAt.AddHours(-3)));
        Assert.Equal(new DateOnly(2026, 9, 2), DateOnly.FromDateTime(plan.Customers[2].SaleAt.AddHours(-3)));
        Assert.Equal(5, plan.Trips.Length);
        Assert.DoesNotContain(plan.Trips, t => DateOnly.FromDateTime(t.OpenedAt.AddHours(-3)) == new DateOnly(2026, 10, 7));
        var routes = real.Concat(plan.Trips.Select(t => new RouteDeparture(customers[0].VehicleId!.Value, t.ZoneId, [t.Day], t.OpenedAt))).ToArray();
        Assert.Equal(new[] { 0, 3, 5 }, plan.Customers.Select(p => CustomerActivityPolicy.MissedWeeks(p.Customer, p.SaleAt, routes)));
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 3, 1)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 10, 5)]
    public void AllSingleVisitDaysAcrossWeekAndYearBoundaries(int year, int month, int day)
    {
        var now = new DateTime(year, month, day, 1, 0, 0, DateTimeKind.Utc);
        for (var a = 1; a <= 7; a++)
        for (var b = 1; b <= 7; b++)
        for (var c = 1; c <= 7; c++)
        {
            var plan = Planner.Create(now, Customers(false, [a], [b], [c]));
            AssertPolicy(plan);
            Assert.All(plan.Trips, t => { Assert.True(t.ClosedAt < now); Assert.Equal(DateTimeKind.Utc, t.OpenedAt.Kind); });
            // One week closer when this week's visit day already passed and counts as a turn.
            var monday = BusinessTime.WeekStart(now);
            Assert.Contains(BusinessTime.WeekStart(plan.Customers[1].SaleAt), new[] { monday.AddDays(-21), monday.AddDays(-28) });
            Assert.Contains(BusinessTime.WeekStart(plan.Customers[2].SaleAt), new[] { monday.AddDays(-35), monday.AddDays(-42) });
            var trips = plan.Trips.OrderBy(t => t.OpenedAt).ToArray();
            for (var i = 1; i < trips.Length; i++) Assert.True(trips[i - 1].ClosedAt < trips[i].OpenedAt);
        }
    }

    [Fact]
    public void KilometerAllocationFitsExistingHistoryAndDoesNotChangeOdometer()
    {
        var plan = Planner.Create(new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc), Customers(true));
        DeliverySession[] real = [new() { OpenedAt = plan.Trips[0].OpenedAt.AddDays(-1), KilometersAtOpen = 70, KilometersAtClose = 80 },
            new() { OpenedAt = plan.Trips[3].OpenedAt.AddDays(-1), KilometersAtOpen = 90, KilometersAtClose = 95 }];
        var km = Planner.Kilometers(plan.Trips, real, 100);
        Assert.All(km.Values, x => { Assert.Equal(x.Open + 1, x.Close); Assert.InRange(x.Open, 80, 99); });
        Assert.True(km[plan.Trips[2].OpenedAt].Close <= 90);
        Assert.True(km[plan.Trips[3].OpenedAt].Open >= 95);
        Assert.Throws<InvalidOperationException>(() => Planner.Kilometers(plan.Trips, [], 0));
    }

    [Theory]
    [InlineData(ContainerTracking.None)]
    [InlineData(ContainerTracking.ByBalance)]
    public void BuiltRowsBalanceStockCashAndContainersWithoutDatabase(ContainerTracking tracking)
    {
        var customers = Customers(true);
        var plan = Planner.Create(new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc), customers);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var rows = SeedBuilder.Build(db, plan, new Driver { Id = Guid.NewGuid(), VehicleId = customers[0].VehicleId,
            ApplicationUserId = Guid.NewGuid() }, Guid.NewGuid(), new Product { Id = Guid.NewGuid(), Tracking = tracking },
            customers.ToDictionary(c => c.Id, _ => 12.34m), Planner.Kilometers(plan.Trips, [], 1000), Guid.NewGuid());
        Assert.Equal(3, rows.OfType<Delivery>().Count());
        Assert.All(rows, r => Assert.NotEqual(Guid.Empty, r.Id));
        Assert.All(rows.OfType<SessionStockMovement>().GroupBy(m => (m.DeliverySessionId, m.ProductId, m.State)), g => Assert.Equal(0, g.Sum(m => m.Quantity)));
        Assert.All(rows.OfType<ContainerMovement>().GroupBy(m => m.CustomerId), g => Assert.Equal(0, g.Sum(m => m.Quantity)));
        Assert.Empty(rows.OfType<CustomerContainerBalance>());
        foreach (var session in rows.OfType<DeliverySession>())
        {
            var payments = rows.OfType<Payment>().Where(p => p.DeliverySessionId == session.Id).Sum(p => p.Amount);
            Assert.Equal(payments, rows.OfType<Delivery>().Where(d => d.DeliverySessionId == session.Id).Sum(d => d.Total));
            Assert.Equal(payments, rows.OfType<SessionCashSettlement>().Single(s => s.DeliverySessionId == session.Id).AmountReceived);
            Assert.Equal(SessionStatus.Closed, session.Status);
            Assert.Equal(session.OpenedAt, session.CreatedAt);
        }
    }
}
