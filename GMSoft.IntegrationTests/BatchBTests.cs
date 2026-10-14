using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.Update;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Products.Delete;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class BatchBTests(PostgresFixture fixture)
{
    [Theory, InlineData(0), InlineData(25)]
    public async Task Cash_difference_ignores_transfer_and_card_in_all_three_views(int card)
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(1, paid: true));
        await f.Send(f.Sale(1) with { Items = [new(f.NoneId, 1)], Payment = new(null, PaymentMethod.Transfer) });
        if (card > 0) await f.Send(f.Sale(1) with { Payment = new(card, PaymentMethod.Card) });
        Assert.Null((await f.Send(new GetSessionSettlementQuery(f.SessionId), true)).CashDifference);
        await f.Send(f.Close(card > 0 ? 18 : 19) with { Returns = [] }, true);
        var post = await f.Send(new RegisterSettlementCommand(f.SessionId, 100, null), true);
        var get = await f.Send(new GetSessionSettlementQuery(f.SessionId), true);
        var opened = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        var date = DateOnly.FromDateTime(opened.AddHours(-3));
        var daily = await f.Send(new GetDailySummaryQuery(f.VehicleId, date), true);
        Assert.Equal(0, post.CashDifference);
        Assert.Equal(post, get);
        Assert.Equal(150 + card, get.TotalCollected);
        Assert.Equal(card > 0 ? 75 : 0, get.NewDebt);
        Assert.Equal(0, Assert.Single(daily.Sessions).Money.CashDifference);
        Assert.Equal(0, daily.DayTotals.Money.CashDifference);
    }

    [Theory]
    [InlineData(DeliveryType.Sale, false)]
    [InlineData(DeliveryType.Sale, true)]
    [InlineData(DeliveryType.NoPurchase, false)]
    [InlineData(DeliveryType.NoPurchase, true)]
    public async Task Visit_preserves_admin_edit_after_read(DeliveryType type, bool reassign)
    {
        var f = await new Scenario(fixture).Seed();
        var other = await new Scenario(fixture).Seed(false);
        var barrier = new SqlReadBarrier("Customers", "LIMIT", manualRelease: true);
        var command = type == DeliveryType.Sale ? f.Sale(1) : f.Sale() with
        { Type = type, Items = [], NoPurchaseReason = NoPurchaseReason.NotHome };
        var visit = f.Send(command, barrier: barrier);
        try
        {
            await barrier.WaitForReadAsync();
            await f.Send(new UpdateCustomerCommand(f.CustomerId, "ADMIN", "Editado", "123", "Nueva calle",
                null, reassign ? other.ZoneId : f.ZoneId, 19, "Conservar", reassign, [2, 4],
                reassign ? other.VehicleId : f.VehicleId), true);
        }
        finally { barrier.Release(); }
        var result = await visit;
        var customer = await f.Read(db => db.Customers.SingleAsync(c => c.Id == f.CustomerId));
        Assert.Equal(reassign, customer.IsActive);
        Assert.Equal(reassign ? other.VehicleId : f.VehicleId, customer.VehicleId);
        Assert.Equal(reassign ? other.ZoneId : f.ZoneId, customer.ZoneId);
        Assert.Equal("Editado", customer.ContactName);
        Assert.Equal("Nueva calle", customer.Address);
        Assert.Equal("Conservar", customer.Notes);
        Assert.Equal(new[] { 2, 4 }, customer.VisitDays);
        var delivery = await f.Read(db => db.Deliveries.SingleAsync(d => d.Id == result.DeliveryId));
        Assert.Equal(delivery.DeliveredAt, customer.LastVisitAt);
        Assert.True(customer.UpdatedAt >= customer.LastVisitAt);
    }

    [Theory, InlineData("load"), InlineData("unit"), InlineData("positive"), InlineData("negative")]
    public async Task Live_product_references_prevent_deletion(string reference)
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Write(db =>
        {
            if (reference == "load") db.VehicleLoads.Add(new VehicleLoad { ProductId = f.ProductId,
                VehicleId = f.VehicleId, Quantity = 1, LoadedAt = DateTime.UtcNow });
            else if (reference == "unit") db.ContainerUnits.Add(new ContainerUnit { ProductId = f.ProductId, SerialNumber = Guid.NewGuid().ToString() });
            else db.CustomerContainerBalances.Add(new CustomerContainerBalance { ProductId = f.ProductId,
                CustomerId = f.CustomerId, Quantity = reference == "positive" ? 1 : -1 });
            return Task.CompletedTask;
        });
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Send(new DeleteProductCommand(f.ProductId), true));
        Assert.Contains("Despublicalo", error.Message);
        Assert.True(await f.Read(db => db.Products.AnyAsync(p => p.Id == f.ProductId)));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Product_without_live_references_can_be_deleted(bool zeroBalance)
    {
        var f = await new Scenario(fixture).Seed(false);
        if (zeroBalance) await f.Write(db => { db.CustomerContainerBalances.Add(new CustomerContainerBalance {
            CustomerId = f.CustomerId, ProductId = f.ProductId, Quantity = 0 }); return Task.CompletedTask; });
        await f.Send(new DeleteProductCommand(f.ProductId), true);
        Assert.False(await f.Read(db => db.Products.AnyAsync(p => p.Id == f.ProductId)));
    }

    [Theory]
    [InlineData(DeliveryType.Sale, false), InlineData(DeliveryType.Sale, true)]
    [InlineData(DeliveryType.ContainerOnly, false), InlineData(DeliveryType.ContainerOnly, true)]
    [InlineData(DeliveryType.NoPurchase, false), InlineData(DeliveryType.NoPurchase, true)]
    public async Task Every_visit_requires_a_nonempty_request_id(DeliveryType type, bool empty)
    {
        var f = await new Scenario(fixture).Seed();
        var request = f.Sale() with { Type = type, ClientRequestId = empty ? Guid.Empty : null };
        if (type != DeliveryType.Sale) request = request with { Items = [],
            ContainersIn = type == DeliveryType.ContainerOnly ? [new(f.ProductId, 1)] : [],
            NoPurchaseReason = type == DeliveryType.NoPurchase ? NoPurchaseReason.NotHome : null };
        var error = await Assert.ThrowsAsync<ValidationException>(() => f.Send(request));
        Assert.Contains("ClientRequestId", error.Errors.Keys);
        Assert.Equal(0, await f.Sales());
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Extreme_session_dates_are_validation_errors(bool closed)
    {
        var f = await new Scenario(fixture).Seed(false);
        var query = closed ? new GetSessionsQuery(ClosedDate: DateOnly.MaxValue) : new GetSessionsQuery(Date: DateOnly.MaxValue);
        var error = await Assert.ThrowsAsync<ValidationException>(() => f.Send(query, true));
        Assert.Contains(closed ? "ClosedDate" : "Date", error.Errors.Keys);
    }

    [Fact]
    public async Task Unknown_return_product_is_not_found_before_any_changes()
    {
        var f = await new Scenario(fixture).Seed();
        var missing = Guid.NewGuid();
        var command = f.Close() with { Returns = [new(f.ProductId, ContainerState.Full, 1), new(missing, ContainerState.Empty, 1)] };
        await Assert.ThrowsAsync<NotFoundException>(() => f.Send(command, true));
        Assert.Equal(SessionStatus.Open, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.Status).SingleAsync()));
        Assert.Equal(100, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        Assert.Equal(20, await f.Stock());
        Assert.False(await f.Read(db => db.SessionStockMovements.AnyAsync(m => m.DeliverySessionId == f.SessionId && m.Type == SessionStockMovementType.ReturnedAtClose)));
    }
}
