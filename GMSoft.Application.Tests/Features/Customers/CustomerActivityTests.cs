using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class CustomerActivityTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "White")]
    [InlineData(14, "White")]
    [InlineData(15, "Red")]
    [InlineData(29, "Red")]
    [InlineData(30, "Black")]
    [InlineData(100, "Black")]
    public void Purchase_age_uses_inclusive_thresholds(int days, string expected)
    {
        var customer = new Customer { CreatedAt = Now.AddDays(-200) };
        var dto = CustomerMapping.ToDto(customer, Now.AddDays(-days), new(15, 30), Now);
        Assert.Equal(expected, dto.ActivityStatus);
        Assert.Equal(days, dto.DaysWithoutPurchase);
    }

    [Fact]
    public void New_purchase_resets_black_customer_to_white()
    {
        var customer = new Customer { CreatedAt = Now.AddDays(-200) };
        Assert.Equal("White", CustomerMapping.ToDto(customer, Now, new(15, 30), Now).ActivityStatus);
    }

    [Fact]
    public void Never_purchased_ages_from_creation_without_inventing_a_purchase()
    {
        var customer = new Customer { CreatedAt = Now.AddDays(-15) };
        var dto = CustomerMapping.ToDto(customer, null, new(15, 30), Now);
        Assert.Equal("Red", dto.ActivityStatus);
        Assert.Null(dto.DaysWithoutPurchase);
        Assert.True(dto.SinComprasRegistradas);
    }

    [Fact]
    public void Thresholds_can_change_without_changing_purchase_history()
    {
        var customer = new Customer { CreatedAt = Now.AddDays(-200) };
        Assert.Equal("White", CustomerMapping.ToDto(customer, Now.AddDays(-20), new(25, 60), Now).ActivityStatus);
    }

    [Fact]
    public void Day_changes_at_Argentina_midnight_not_utc_midnight()
    {
        var purchase = new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, CustomerActivityPolicy.DaysSince(purchase, purchase.AddHours(1)));
        Assert.Equal(1, CustomerActivityPolicy.DaysSince(purchase, purchase.AddHours(2)));
        Assert.Equal(0, CustomerActivityPolicy.DaysSince(purchase.AddDays(1), purchase));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(15, 15)]
    [InlineData(30, 15)]
    public void Invalid_thresholds_are_rejected(int red, int black)
        => Assert.Throws<ArgumentException>(() => new CustomerActivityPolicy(red, black));
}
