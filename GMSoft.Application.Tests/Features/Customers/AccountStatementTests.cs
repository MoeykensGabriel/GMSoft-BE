using GMSoft.Application.Features.Customers.Account;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Customers;

public class AccountStatementTests
{
    private static readonly DateTime Date = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static Delivery Sale() => new() { CustomerId = Guid.NewGuid(), DeliverySessionId = Guid.NewGuid(),
        Type = DeliveryType.Sale, DeliveredAt = Date, Total = 20000,
        Items = [new() { Product = new() { Detail = "Bidon" }, Quantity = 4, UnitPrice = 5000 }] };

    [Theory]
    [InlineData(20000, PaymentMethod.Cash, 0)]
    [InlineData(0, PaymentMethod.Cash, 20000)]
    [InlineData(5000, PaymentMethod.Transfer, 15000)]
    [InlineData(20000, PaymentMethod.Card, 0)]
    public void Sale_groups_payment_and_preserves_frozen_price(int paid, PaymentMethod method, int balance)
    {
        var d = Sale();
        var result = AccountStatementBuilder.Build([d], paid == 0 ? [] : [new() {
            CustomerId = d.CustomerId, DeliverySessionId = d.DeliverySessionId, PaidAt = Date, Amount = paid, Method = method }], [], [], 60);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Sale", row.Kind); Assert.Equal(20000, row.Charge); Assert.Equal(paid, row.Paid);
        Assert.Equal(balance, row.BalanceAfter); Assert.Equal(balance, result.Balance);
        Assert.Equal(5000, Assert.Single(row.Lines).UnitPrice); Assert.Equal(20000, row.Lines[0].Amount);
        if (paid > 0) Assert.Equal(method, Assert.Single(row.Payments).Method);
    }

    [Fact]
    public void Standalone_payment_and_nonfinancial_rows_preserve_full_balance_when_limited()
    {
        var d = Sale();
        var noPurchase = new Delivery { Type = DeliveryType.NoPurchase, DeliveredAt = Date.AddDays(2),
            NoPurchaseReason = NoPurchaseReason.NotHome, Notes = "No estaba", Total = 999 };
        var result = AccountStatementBuilder.Build([d, noPurchase], [new() { CustomerId = d.CustomerId,
            DeliverySessionId = d.DeliverySessionId, PaidAt = Date.AddDays(1), Amount = 3000, Method = PaymentMethod.Cash }],
            [new() { Product = new() { Detail = "Bidon" }, Type = ContainerMovementType.Adjustment,
                Quantity = -2, Notes = "Recuento", OccurredAt = Date.AddDays(3) }], [], 3);
        Assert.Equal(4, result.TotalCount); Assert.Equal(3, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal(17000, r.BalanceAfter));
        Assert.Equal(result.Balance, result.Rows[0].BalanceAfter);
        Assert.Equal(-2, Assert.Single(result.Rows[0].ContainerChanges).Quantity);
        Assert.Equal("Recuento", result.Rows[0].ContainerChanges[0].Reason);
        Assert.Equal(NoPurchaseReason.NotHome, result.Rows[1].NoPurchaseReason);
        Assert.Equal(0, result.Rows[1].Charge); Assert.Equal("Payment", result.Rows[2].Kind);
    }

    [Fact]
    public void Ambiguous_same_instant_visits_do_not_duplicate_or_guess_payment()
    {
        var d = Sale(); var second = Sale(); second.CustomerId = d.CustomerId; second.DeliverySessionId = d.DeliverySessionId;
        var result = AccountStatementBuilder.Build([d, second], [new() { CustomerId = d.CustomerId,
            DeliverySessionId = d.DeliverySessionId, PaidAt = Date, Amount = 5000 }], [], [], 60);
        Assert.Equal(3, result.TotalCount); Assert.Equal(35000, result.Balance);
        Assert.Single(result.Rows, r => r.Kind == "Payment");
        Assert.Equal(5000, result.Rows.Sum(r => r.Paid));
    }

    [Theory]
    [InlineData(0, false)][InlineData(201, false)][InlineData(1, true)][InlineData(200, true)]
    public void Statement_limit_is_validated(int limit, bool valid)
        => Assert.Equal(valid, new GetCustomerAccountQueryValidator().Validate(new GetCustomerAccountQuery(Guid.NewGuid(), StatementLimit: limit)).IsValid);

    [Fact]
    public void Empty_account_has_zero_balance_and_count()
    {
        var result = AccountStatementBuilder.Build([], [], [], [], 60);
        Assert.Empty(result.Rows); Assert.Equal(0, result.TotalCount); Assert.Equal(0, result.Balance);
    }
}
