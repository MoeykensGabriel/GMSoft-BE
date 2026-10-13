using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Reports.ContainersOut;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class HistoricalPromotionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Registration_rejects_historical_type_without_side_effects()
    {
        var f = await new Scenario(fixture).Seed();
        var error = await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.ValidationException>(
            () => f.Send(f.Sale() with { Type = DeliveryType.Promotion }));
        Assert.Contains(error.Errors.SelectMany(e => e.Value), message => message.Contains("POST /api/promotions"));
        Assert.Equal(0, await f.Sales());
        Assert.Equal(20, await f.Stock());
        Assert.Equal(0, await f.Balance());
    }

    [Fact]
    public async Task Historical_promotion_remains_visible_in_route_settlement_summary_report_and_account()
    {
        var f = await new Scenario(fixture).Seed();
        var date = DateTime.UtcNow;
        var delivery = new Delivery
        {
            DeliverySessionId = f.SessionId, CustomerId = f.CustomerId,
            Type = DeliveryType.Promotion, DeliveredAt = date, Total = 0,
            Items = [new DeliveryItem { ProductId = f.ProductId, Quantity = 2, UnitPrice = 0 }],
            ContainerMovements = [new ContainerMovement { CustomerId = f.CustomerId,
                ProductId = f.ProductId, Quantity = 2, Type = ContainerMovementType.DeliveredToCustomer,
                OccurredAt = date }]
        };
        // Seed the old ledger directly: new registrations must never create this type.
        await f.Write(db =>
        {
            db.Deliveries.Add(delivery);
            db.SessionStockMovements.Add(new SessionStockMovement { DeliverySessionId = f.SessionId,
                ProductId = f.ProductId, State = ContainerState.Full, Quantity = -2,
                Type = SessionStockMovementType.Delivered, OccurredAt = date });
            db.CustomerContainerBalances.Add(new CustomerContainerBalance {
                CustomerId = f.CustomerId, ProductId = f.ProductId, Quantity = 2 });
            return Task.CompletedTask;
        });

        var route = Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId)));
        Assert.Equal(DeliveryType.Promotion, route.Type);
        Assert.Equal(2, Assert.Single(route.Items).Quantity);
        Assert.Equal(0, route.Total);
        var settlement = Assert.Single(await f.Send(new GetSessionDetailedSettlementQuery(f.SessionId), true));
        var line = Assert.Single(settlement.Lines);
        Assert.Equal(DeliveryType.Promotion, line.Type);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(0, line.Amount);
        Assert.Equal(0, settlement.Balance);
        var summary = await f.Send(new GetDailySummaryQuery(f.VehicleId,
            DateOnly.FromDateTime(date.AddHours(-3))), true);
        var product = Assert.Single(summary.DayTotals.Products, p => p.ProductId == f.ProductId);
        Assert.Equal(2, product.Stock.FullSold);
        Assert.Equal(18, product.Stock.FullDifference);
        Assert.Equal(0, summary.DayTotals.Money.CashExpected);
        var report = Assert.Single(await f.Send(new GetContainersOutReportQuery(), true), p => p.ProductId == f.ProductId);
        Assert.Equal(2, report.QuantityOut);
        Assert.Equal(1, report.CustomersHolding);
        Assert.Equal(0, report.PromotionQuantityOut);
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId), true);
        Assert.Equal(0, account.Balance);
        Assert.Null(account.LastPurchaseAt);
        Assert.Equal(2, Assert.Single(account.Containers).Quantity);
        var movement = Assert.Single(account.Movements);
        Assert.Equal(delivery.Id, movement.ReferenceId);
        Assert.Equal(0, movement.Amount);
    }
}
