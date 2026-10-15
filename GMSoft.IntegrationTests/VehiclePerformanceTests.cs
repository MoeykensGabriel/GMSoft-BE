using System.Reflection;
using System.Text.Json;
using GMSoft.API.Controllers;
using GMSoft.Application.Features.Reports.VehiclePerformance;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehiclePerformanceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Report_aggregates_all_metrics_and_preserves_idle_and_historical_vehicles()
    {
        var a = await new Scenario(fixture).Seed(open: false);
        var b = await new Scenario(fixture).Seed(open: false);
        var idle = await new Scenario(fixture).Seed(open: false);
        var absent = await new Scenario(fixture).Seed(open: false);
        var start = new DateTime(2031, 5, 10, 3, 0, 0, DateTimeKind.Utc);
        var customer2 = a.Customer(8);
        var customer3 = a.Customer(9);
        var inactive = a.Customer(10); inactive.IsActive = false;
        var first = Trip(a, start, true, 100, 125);
        var last = Trip(a, start.AddDays(2).AddTicks(-10), false, 125, null);
        var other = Trip(b, start.AddHours(2), true, 100, 110);
        var before = Trip(a, start.AddTicks(-10), true, 0, 1000);
        var after = Trip(a, start.AddDays(2), true, 0, 1000);
        await a.Write(db =>
        {
            db.Customers.AddRange(customer2, customer3, inactive);
            db.DeliverySessions.AddRange(first, last, other, before, after);
            void Visit(DeliverySession trip, Guid customer, DeliveryType type, decimal total = 0,
                NoPurchaseReason? reason = null, int units = 0, Guid? product = null)
            {
                db.Deliveries.Add(new Delivery { DeliverySessionId = trip.Id, CustomerId = customer,
                    Type = type, Total = total, NoPurchaseReason = reason, DeliveredAt = trip.OpenedAt.AddHours(5),
                    Items = units == 0 ? [] : [new() { ProductId = product ?? a.ProductId, Quantity = units, UnitPrice = total / units }] });
            }
            Visit(first, a.CustomerId, DeliveryType.Sale, 200, units: 2);
            Visit(first, customer2.Id, DeliveryType.Sale, 300, units: 3);
            Visit(last, a.CustomerId, DeliveryType.Sale, 100, units: 1);
            Visit(first, customer3.Id, DeliveryType.NoPurchase, reason: NoPurchaseReason.NotHome);
            Visit(last, customer3.Id, DeliveryType.NoPurchase, reason: NoPurchaseReason.NoMoney);
            Visit(last, customer2.Id, DeliveryType.NoPurchase, reason: NoPurchaseReason.NotHome);
            Visit(other, b.CustomerId, DeliveryType.Sale, 1000, units: 5, product: b.ProductId);
            Visit(before, a.CustomerId, DeliveryType.Sale, 9999);
            Visit(after, a.CustomerId, DeliveryType.Sale, 9999);
            void Pay(DeliverySession trip, Guid customer, decimal amount, PaymentMethod method) => db.Payments.Add(
                new() { DeliverySessionId = trip.Id, CustomerId = customer, Amount = amount, Method = method, PaidAt = trip.OpenedAt });
            Pay(first, a.CustomerId, 200, PaymentMethod.Cash);
            Pay(first, customer2.Id, 300, PaymentMethod.Transfer);
            Pay(first, customer3.Id, 50, PaymentMethod.Cash); // debt collection, no sale
            Pay(last, customer3.Id, 25, PaymentMethod.Card);
            Pay(other, b.CustomerId, 1200, PaymentMethod.Transfer);
            Pay(before, a.CustomerId, 9999, PaymentMethod.Cash);
            db.Payments.Add(new() { CustomerId = a.CustomerId, Amount = 9999, Method = PaymentMethod.Cash, PaidAt = start });
            db.SessionCashSettlements.AddRange(new SessionCashSettlement { DeliverySessionId = first.Id, AmountReceived = 240, ReceivedAt = start.AddDays(3) },
                new SessionCashSettlement { DeliverySessionId = other.Id, AmountReceived = 0, ReceivedAt = start });
            void Move(DeliverySession trip, int quantity, ContainerState state, SessionStockMovementType type) =>
                db.SessionStockMovements.Add(new() { DeliverySessionId = trip.Id, ProductId = a.ProductId,
                    Quantity = quantity, State = state, Type = type });
            Move(first, 10, ContainerState.Full, SessionStockMovementType.InitialLoad);
            Move(first, 2, ContainerState.Full, SessionStockMovementType.Restock);
            Move(first, -5, ContainerState.Full, SessionStockMovementType.Delivered);
            Move(first, -1, ContainerState.Full, SessionStockMovementType.Adjustment);
            Move(first, -4, ContainerState.Full, SessionStockMovementType.ReturnedAtClose);
            Move(first, 5, ContainerState.Empty, SessionStockMovementType.CollectedEmpty);
            Move(first, -2, ContainerState.Empty, SessionStockMovementType.ReturnedAtClose);
            Move(last, 999, ContainerState.Full, SessionStockMovementType.InitialLoad); // open stock is not shortage
            return Task.CompletedTask;
        });
        await b.Write(async db => (await db.Vehicles.FindAsync(b.VehicleId))!.IsDeleted = true);
        await absent.Write(async db => (await db.Vehicles.FindAsync(absent.VehicleId))!.IsDeleted = true);
        var report = await a.Send(new GetVehiclePerformanceQuery(new(2031, 5, 10), new(2031, 5, 11)), admin: true);
        var x = Assert.Single(report.Vehicles, v => v.VehicleId == a.VehicleId);
        Assert.Equal("Camion", x.VehicleName); Assert.Equal(a.VehicleId.ToString("N")[..12], x.LicensePlate);
        Assert.Equal(new[] { "Juan Prueba" }, x.DriverNames);
        Assert.Equal(2, x.Sessions); Assert.Equal(1, x.ClosedSessions);
        Assert.Equal(600m, x.Sold); Assert.Equal(575m, x.Collected); Assert.Equal(25m, x.OnAccount);
        Assert.Equal(new CollectedByMethodDto(250, 300, 25), x.CollectedByMethod);
        Assert.Equal(575m / 600, x.CollectedPercent);
        Assert.Equal(3, x.RouteCustomers); Assert.Equal(3, x.VisitedCustomers); Assert.Equal(2, x.BuyingCustomers);
        Assert.Equal(2m / 3, x.BuyingPercent); Assert.Equal(6, x.Visits); Assert.Equal(3, x.Sales);
        Assert.Equal(200m, x.AverageTicket); Assert.Equal(3, x.NoPurchase.Total);
        Assert.Equal(2, x.NoPurchase.ByReason["NotHome"]); Assert.Equal(1, x.NoPurchase.ByReason["NoMoney"]);
        Assert.Equal(new ProductPerformanceDto(a.ProductId, "Bidon", 6, 600), Assert.Single(x.UnitsByProduct));
        Assert.Equal(new ShortagesDto(2, 3), x.Shortages); Assert.Equal(10m, x.CashDifference);
        Assert.Equal(25, x.Kilometers); Assert.Equal(24m, x.SoldPerKilometer);
        var y = Assert.Single(report.Vehicles, v => v.VehicleId == b.VehicleId);
        Assert.Equal(1000m, y.Sold); Assert.Equal(1200m, y.Collected); Assert.Equal(-200m, y.OnAccount);
        Assert.Equal(1.2m, y.CollectedPercent); Assert.Equal(0m, y.CashDifference);
        Assert.Equal(10, y.Kilometers); Assert.Equal(100m, y.SoldPerKilometer);
        Assert.Equal(b.VehicleId, report.Vehicles[0].VehicleId);
        var zero = Assert.Single(report.Vehicles, v => v.VehicleId == idle.VehicleId);
        Assert.Equal(0, zero.Sessions); Assert.Equal(0, zero.ClosedSessions); Assert.Empty(zero.DriverNames);
        Assert.Equal(0m, zero.Sold); Assert.Equal(0m, zero.Collected); Assert.Equal(0m, zero.OnAccount);
        Assert.Equal(new CollectedByMethodDto(0, 0, 0), zero.CollectedByMethod);
        Assert.Equal(1, zero.RouteCustomers); Assert.Equal(0, zero.VisitedCustomers); Assert.Equal(0, zero.BuyingCustomers);
        Assert.Equal(0, zero.Visits); Assert.Equal(0, zero.Sales); Assert.Equal(0, zero.NoPurchase.Total);
        Assert.Empty(zero.NoPurchase.ByReason); Assert.Empty(zero.UnitsByProduct);
        Assert.Equal(new ShortagesDto(0, 0), zero.Shortages); Assert.Equal(0m, zero.CashDifference); Assert.Equal(0, zero.Kilometers);
        Assert.Null(zero.CollectedPercent); Assert.Null(zero.BuyingPercent); Assert.Null(zero.AverageTicket); Assert.Null(zero.SoldPerKilometer);
        Assert.DoesNotContain(report.Vehicles, v => v.VehicleId == absent.VehicleId);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("2031-05-10", json.RootElement.GetProperty("from").GetString());
        Assert.Equal(1000m, json.RootElement.GetProperty("vehicles")[0].GetProperty("sold").GetDecimal());
    }

    [Fact]
    public void Endpoint_is_get_and_admin_only()
    {
        Assert.Equal("Admin", typeof(ReportsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("vehicle-performance", typeof(ReportsController).GetMethod("VehiclePerformance")!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public async Task Invalid_range_is_rejected_by_application_pipeline()
    {
        var scenario = new Scenario(fixture);
        await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.ValidationException>(() => scenario.Send(
            new GetVehiclePerformanceQuery(DateOnly.MaxValue, DateOnly.MaxValue), admin: true));
    }

    private static DeliverySession Trip(Scenario s, DateTime opened, bool closed, int kmOpen, int? kmClose) => new()
    {
        Id = Guid.NewGuid(), VehicleId = s.VehicleId, DriverId = s.DriverId, ZoneId = s.ZoneId,
        OpenedAt = opened, ClosedAt = closed ? opened.AddHours(8) : null,
        Status = closed ? SessionStatus.Closed : SessionStatus.Open, KilometersAtOpen = kmOpen, KilometersAtClose = kmClose
    };
}
