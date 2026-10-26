using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Customers.OutOfRoute;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class OutOfRouteTests(PostgresFixture fixture)
{
    private static Task AnotherDay(Scenario f) => f.Write(async db =>
        (await db.Customers.SingleAsync(c => c.Id == f.CustomerId)).VisitDays = [4]);

    [Fact]
    public async Task Sale_moves_stock_and_balances_and_exposes_flag_and_count_without_duplicate_retries()
    {
        var f = await new Scenario(fixture).Seed();
        await AnotherDay(f);
        var command = f.Sale() with { OutOfRoute = true };
        var result = await f.Send(command);
        Assert.Equal(result, await f.Send(command));
        Assert.Equal(17, await f.Stock());
        Assert.Equal(3, await f.Balance());
        Assert.Equal(300, result.SaldoCuentaCliente);
        Assert.True(await f.Read(db => db.Deliveries.Where(d => d.Id == result.DeliveryId).Select(d => d.IsOutOfRoute).SingleAsync()));
        Assert.True(Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId))).IsOutOfRoute);
        Assert.True(Assert.Single(await f.Send(new GetSessionDetailedSettlementQuery(f.SessionId), true)).IsOutOfRoute);
        var date = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        Assert.Equal(1, Assert.Single((await f.Send(new GetDailySummaryQuery(f.VehicleId,
            DateOnly.FromDateTime(date + BusinessTime.Offset)), true)).Sessions).OutOfRouteCount);
        Assert.Equal(300, (await f.Send(new GetCustomerAccountQuery(f.CustomerId, 50))).Balance);
        Assert.NotNull(await f.Send(new GetCustomerByIdQuery(f.CustomerId)));
        Assert.Empty(await f.Send(new GetCustomerPricesQuery(f.CustomerId)));
        Assert.Empty((await f.Send(new GetCustomersQuery())).Items);
        Assert.Equal(f.CustomerId, Assert.Single(await f.Send(new GetOutOfRouteCustomersQuery("Cliente"))).Id);
    }

    [Fact]
    public async Task Container_only_recovers_unregistered_empties_without_negative_balance()
    {
        var f = await new Scenario(fixture).Seed();
        await AnotherDay(f);
        var result = await f.Send(f.Sale() with { OutOfRoute = true, Type = DeliveryType.ContainerOnly,
            Items = [], ContainersIn = [new(f.ProductId, 2)] });
        Assert.Equal(0, await f.Balance());
        Assert.Equal(2, await f.Stock(ContainerState.Empty));
        Assert.Equal(20, await f.Stock());
        var correction = await f.Read(db => db.ContainerMovements.SingleAsync(m => m.DeliveryId == result.DeliveryId
            && m.Type == ContainerMovementType.ExchangeCorrection));
        Assert.Equal("Tenía sin registrar", correction.Notes);
        Assert.Equal(2, correction.Quantity);
        Assert.True(Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId))).IsOutOfRoute);
        Assert.True(Assert.Single(await f.Send(new GetSessionDetailedSettlementQuery(f.SessionId), true)).IsOutOfRoute);
        var date = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        var summary = Assert.Single((await f.Send(new GetDailySummaryQuery(f.VehicleId,
            DateOnly.FromDateTime(date + BusinessTime.Offset)), true)).Sessions);
        Assert.Equal(1, summary.OutOfRouteCount);
        Assert.Equal(2, summary.Totals.EmptyRecovered);
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("inactive")]
    [InlineData("noPurchase")]
    [InlineData("newCustomer")]
    public async Task Rejects_invalid_exceptions_without_writes(string reason)
    {
        var f = await new Scenario(fixture).Seed();
        await AnotherDay(f);
        var command = f.Sale() with { OutOfRoute = true };
        if (reason == "vehicle")
        {
            var other = await new Scenario(fixture).Seed(open: false);
            command = command with { CustomerId = other.CustomerId };
            await Assert.ThrowsAsync<BadRequestException>(() => f.Send(new GetCustomerAccountQuery(other.CustomerId, 50)));
        }
        if (reason == "inactive")
            await f.Write(async db => (await db.Customers.SingleAsync(c => c.Id == f.CustomerId)).IsActive = false);
        if (reason == "noPurchase")
            command = command with { Type = DeliveryType.NoPurchase, Items = [], NoPurchaseReason = NoPurchaseReason.NotHome };
        if (reason == "newCustomer")
            command = command with { CustomerId = null, NewCustomer = new(null, "Nuevo", "123", "Calle", null, [4]) };
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(command));
        Assert.Equal(0, await f.Sales());
        Assert.Equal(20, await f.Stock());
    }

    [Fact]
    public async Task Matching_customer_stays_normal_and_opt_in_is_required_for_other_days()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale() with { OutOfRoute = true });
        Assert.False(Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId))).IsOutOfRoute);
        await AnotherDay(f);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(f.Sale()));
    }

    [Fact]
    public async Task Search_filters_scope_pending_removals_and_literal_terms_and_limits_results()
    {
        var f = await new Scenario(fixture).Seed();
        var other = await new Scenario(fixture).Seed(open: false);
        await AnotherDay(other);
        var wanted = f.Customer(1, [4]); wanted.BusinessName = "Comercio especial";
        var pending = f.Customer(2, [4]);
        var inactive = f.Customer(3, [4]); inactive.IsActive = false;
        var anotherZone = f.Customer(4, [1]);
        await f.Write(db =>
        {
            var zone = new Zone { Name = "Otra zona" };
            db.Zones.Add(zone); anotherZone.ZoneId = zone.Id;
            db.Customers.AddRange(wanted, pending, inactive, anotherZone);
            db.Set<CustomerRemovalRequest>().Add(new() { CustomerId = pending.Id, DriverId = f.DriverId,
                VehicleId = f.VehicleId, ZoneId = f.ZoneId, RequestedByUserId = f.UserId,
                RequestedAt = DateTime.UtcNow, ClientRequestId = Guid.NewGuid(), Status = RemovalStatus.Pending });
            return Task.CompletedTask;
        });
        var found = await f.Send(new GetOutOfRouteCustomersQuery("cLiEnTe"));
        Assert.Equal(new[] { wanted.Id, anotherZone.Id }.Order(), found.Select(c => c.Id).Order());
        Assert.Equal("Comercio especial", Assert.Single(await f.Send(new GetOutOfRouteCustomersQuery("ESPECIAL"))).Name);
        Assert.Equal(2, (await f.Send(new GetOutOfRouteCustomersQuery("Calle"))).Count);
        Assert.Equal(2, (await f.Send(new GetOutOfRouteCustomersQuery("123"))).Count);
        Assert.Empty(await f.Send(new GetOutOfRouteCustomersQuery("_%")));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Sale() with { CustomerId = pending.Id, OutOfRoute = true }));
        foreach (var term in new[] { "", "a", " a " })
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(new GetOutOfRouteCustomersQuery(term)));
        await f.Write(db => { db.Customers.AddRange(Enumerable.Range(10, 25).Select(i => f.Customer(i, [4]))); return Task.CompletedTask; });
        Assert.Equal(20, (await f.Send(new GetOutOfRouteCustomersQuery("Cliente"))).Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new GetOutOfRouteCustomersQuery("Cliente"), true));
        await f.Write(async db => (await db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId)).Status = SessionStatus.Closed);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new GetOutOfRouteCustomersQuery("Cliente")));
    }

    [Fact]
    public async Task Out_of_day_purchase_resets_activity_and_next_weeks_thursday_counts_again()
    {
        var f = await new Scenario(fixture).Seed();
        await AnotherDay(f);
        var sale = await f.Send(f.Sale() with { OutOfRoute = true });
        var tuesday = new DateTime(2026, 10, 13, 15, 0, 0, DateTimeKind.Utc);
        await f.Write(async db =>
        {
            (await db.Deliveries.SingleAsync(d => d.Id == sale.DeliveryId)).DeliveredAt = tuesday;
            foreach (var day in new[] { -5, 2, 9 })
                db.DeliverySessions.Add(new() { VehicleId = f.VehicleId, DriverId = f.DriverId, ZoneId = f.ZoneId,
                    OpenedAt = tuesday.AddDays(day), ClosedAt = tuesday.AddDays(day).AddHours(1),
                    Status = SessionStatus.Closed, RouteDays = [4] });
        });
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId, 50));
        Assert.Equal(tuesday, account.LastPurchaseAt);
        Assert.Equal(1, account.WeeksWithoutPurchase);
    }
}
