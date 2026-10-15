using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.Removal;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class CustomerRemovalTests
{
    [Theory]
    [InlineData(RemovalReason.Moved)]
    [InlineData(RemovalReason.ClosedBusiness)]
    [InlineData(RemovalReason.NoLongerWants)]
    [InlineData(RemovalReason.Deceased)]
    [InlineData(RemovalReason.Other)]
    public void Valid_reasons_are_accepted(RemovalReason reason)
        => Assert.True(new RequestRemovalValidator().Validate(new RequestRemoval(Guid.NewGuid(), Guid.NewGuid(), reason, "Motivo", false)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Other_requires_note(string? notes)
        => Assert.False(new RequestRemovalValidator().Validate(new RequestRemoval(Guid.NewGuid(), Guid.NewGuid(), RemovalReason.Other, notes, true)).IsValid);

    [Fact]
    public void Invalid_reason_and_missing_key_fail_validation()
        => Assert.False(new RequestRemovalValidator().Validate(new RequestRemoval(Guid.NewGuid(), Guid.Empty, (RemovalReason)99, null, false)).IsValid);

    [Theory]
    [InlineData(RemovalStatus.Pending, 1)]
    [InlineData(RemovalStatus.Confirmed, 2)]
    [InlineData(RemovalStatus.Rejected, 2)]
    public void Removal_history_preserves_money_and_reason(RemovalStatus status, int events)
    {
        var date = DateTime.UtcNow;
        var request = new CustomerRemovalRequest { Id = Guid.NewGuid(), Status = status, Reason = RemovalReason.Moved,
            Notes = "Otra ciudad", RequestedAt = date, ResolvedAt = status == RemovalStatus.Pending ? null : date.AddHours(1) };
        var result = AccountStatementBuilder.Build([], [new() { PaidAt = date.AddDays(-1), Amount = 25 }], [], [], 1, [request]);
        Assert.Equal(events + 1, result.TotalCount);
        var row = Assert.Single(result.Rows);
        Assert.Equal(-25, row.BalanceAfter); Assert.Equal(-25, result.Balance);
        Assert.Equal(0, row.Charge); Assert.Equal(0, row.Paid);
        Assert.Equal(RemovalReason.Moved, row.RemovalReason); Assert.Equal("Otra ciudad", row.Notes);
        Assert.Equal(status == RemovalStatus.Pending ? "RemovalRequested" : "Removal" + status, row.Kind);
    }
}
