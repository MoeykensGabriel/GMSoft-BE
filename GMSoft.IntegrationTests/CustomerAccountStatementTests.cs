using System.Text.Json;
using System.Text.Json.Serialization;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class CustomerAccountStatementTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(200, PaymentMethod.Cash, 0)]
    [InlineData(0, PaymentMethod.Cash, 200)]
    [InlineData(50, PaymentMethod.Transfer, 150)]
    public async Task Registered_visit_groups_real_payment_and_containers(int paid, PaymentMethod method, int balance)
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(2, 2) with { Payment = paid == 0 ? null : new(paid, method) });
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId));
        var row = Assert.Single(account.Statement);
        Assert.Equal("Sale", row.Kind); Assert.Equal(200, row.Charge); Assert.Equal(paid, row.Paid);
        Assert.Equal(balance, account.Balance); Assert.Equal(account.Balance, row.BalanceAfter);
        Assert.Equal(2, Assert.Single(row.ContainersDelivered).Quantity);
        Assert.Equal(2, Assert.Single(row.ContainersReturned).Quantity);
        Assert.Equal("Bidon", Assert.Single(row.Lines).ProductDetail);
        Assert.Equal(100, row.Lines[0].UnitPrice);
        if (paid > 0) Assert.Equal(method, Assert.Single(row.Payments).Method);
        Assert.Equal(paid > 0 ? 2 : 1, account.Movements.Count);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(account, options));
        Assert.Equal(1, json.RootElement.GetProperty("statementTotalCount").GetInt32());
        Assert.Equal("Sale", json.RootElement.GetProperty("statement")[0].GetProperty("kind").GetString());
        Assert.True(json.RootElement.TryGetProperty("movements", out _));
    }

    [Fact]
    public async Task Full_history_includes_debt_collection_no_purchase_admin_changes_and_limit()
    {
        var f = await new Scenario(fixture).Seed();
        var date = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await f.Write(db => {
            db.Deliveries.AddRange(new Delivery { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                Type = DeliveryType.Sale, DeliveredAt = date, Total = 10000 },
                new Delivery { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                    Type = DeliveryType.NoPurchase, DeliveredAt = date.AddDays(2), NoPurchaseReason = NoPurchaseReason.NotHome, Notes = "No estaba" });
            db.Payments.Add(new Payment { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                PaidAt = date.AddDays(1), Amount = 3000, Method = PaymentMethod.Cash, Notes = "Cobro de deuda" });
            db.ContainerMovements.AddRange(new ContainerMovement { CustomerId = f.CustomerId, ProductId = f.ProductId,
                Type = ContainerMovementType.Adjustment, Quantity = 2, Notes = "Recuento", OccurredAt = date.AddDays(3), RegisteredByUserId = f.AdminId },
                new ContainerMovement { CustomerId = f.CustomerId, ProductId = f.ProductId, Type = ContainerMovementType.Lost,
                    Quantity = -1, Notes = "Roto", OccurredAt = date.AddDays(4), RegisteredByUserId = f.AdminId });
            return Task.CompletedTask;
        });
        var full = await f.Send(new GetCustomerAccountQuery(f.CustomerId), true);
        Assert.Equal(5, full.StatementTotalCount); Assert.Equal(7000, full.Balance);
        Assert.Equal(new[] { "ContainerLoss", "ContainerAdjustment", "NoPurchase", "Payment", "Sale" }, full.Statement.Select(r => r.Kind));
        Assert.Equal(-1, Assert.Single(full.Statement[0].ContainerChanges).Quantity);
        Assert.Equal("Roto", full.Statement[0].ContainerChanges[0].Reason);
        Assert.Equal("Recuento", full.Statement[1].ContainerChanges[0].Reason);
        Assert.Equal(NoPurchaseReason.NotHome, full.Statement[2].NoPurchaseReason);
        Assert.Equal("No estaba", full.Statement[2].Notes);
        Assert.Equal(PaymentMethod.Cash, Assert.Single(full.Statement[3].Payments).Method);
        Assert.Equal(10000, full.Statement[^1].BalanceAfter);
        var limited = await f.Send(new GetCustomerAccountQuery(f.CustomerId, StatementLimit: 1));
        Assert.Equal(5, limited.StatementTotalCount);
        Assert.Equal(full.Statement[0].ReferenceId, Assert.Single(limited.Statement).ReferenceId);
        Assert.Equal(limited.Balance, limited.Statement[0].BalanceAfter);
        Assert.Equal(full.Balance, limited.Balance);
        var legacyBalance = await f.Read(async db => await db.Deliveries.Where(d => d.CustomerId == f.CustomerId).SumAsync(d => d.Total)
            - await db.Payments.Where(p => p.CustomerId == f.CustomerId).SumAsync(p => p.Amount));
        Assert.Equal(legacyBalance, full.Balance);
    }
    [Fact]
    public async Task Container_only_legacy_and_converted_promotions_and_office_payment_are_visible()
    {
        var f = await new Scenario(fixture).Seed();
        // Préstamo de envases sin venta: la API ya no lo acepta, pero el historial viejo se sigue viendo.
        var lent = DateTime.UtcNow;
        await f.Write(db => {
            db.Deliveries.Add(new Delivery { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                Type = DeliveryType.ContainerOnly, DeliveredAt = lent, Total = 0, Notes = "Prestamo",
                ContainerMovements = [new ContainerMovement { CustomerId = f.CustomerId, ProductId = f.ProductId,
                    Quantity = 1, Type = ContainerMovementType.DeliveredToCustomer, OccurredAt = lent }] });
            return Task.CompletedTask;
        });
        var date = DateTime.UtcNow.AddDays(-2);
        await f.Write(db => {
            db.Deliveries.Add(new Delivery { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                Type = DeliveryType.Promotion, DeliveredAt = date, Items = [new() { ProductId = f.ProductId, Quantity = 1, UnitPrice = 0 }] });
            db.Promotions.Add(new Promotion { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                VehicleId = f.VehicleId, DriverId = f.DriverId, ZoneId = f.ZoneId, RegisteredAt = date.AddDays(-1),
                PickupDate = DateOnly.FromDateTime(date), Status = PromotionStatus.Converted, ClientRequestId = Guid.NewGuid(),
                Lines = [new() { ProductId = f.ProductId, Quantity = 2, ContainersLoaned = 2 }] });
            db.Payments.Add(new Payment { CustomerId = f.CustomerId, PaidAt = date, Amount = 100, Method = PaymentMethod.Transfer });
            return Task.CompletedTask;
        });
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId));
        Assert.Equal(4, account.StatementTotalCount); Assert.Equal(-100, account.Balance);
        Assert.Equal(account.Balance, account.Statement[0].BalanceAfter);
        Assert.Equal("ContainerOnly", account.Statement[0].Kind);
        Assert.Equal(1, Assert.Single(account.Statement[0].ContainersDelivered).Quantity);
        Assert.Equal(2, account.Statement.Count(r => r.Kind == "Promotion"));
        Assert.Equal(2, Assert.Single(account.Statement[^1].ContainersDelivered).Quantity);
        Assert.Equal(0, account.Statement[^1].Charge);
        Assert.Single(account.Statement, r => r.Kind == "Payment");
        var other = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.BadRequestException>(
            () => other.Send(new GetCustomerAccountQuery(f.CustomerId)));
        Assert.Equal(account.Balance, (await other.Send(new GetCustomerAccountQuery(f.CustomerId), true)).Balance);
    }

}
