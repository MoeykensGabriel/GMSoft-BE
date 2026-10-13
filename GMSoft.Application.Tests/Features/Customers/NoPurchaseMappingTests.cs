using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
namespace GMSoft.Application.Tests.Features.Customers;
public class NoPurchaseMappingTests
{
    [Theory]
    [InlineData(-1, true)][InlineData(0, false)][InlineData(1, false)]
    public void Last_no_purchase_only_after_sale_and_never_resets_activity(int purchaseOffset, bool visible)
    {
        var date = DateTime.UtcNow;
        var result = CustomerMapping.ToDto(new Customer(), date.AddDays(purchaseOffset), 4, new(2, 4), date,
            new(date, NoPurchaseReason.NotHome, null));
        Assert.Equal(visible, result.LastNoPurchase is not null);
        Assert.Equal(4, result.WeeksWithoutPurchase); Assert.Equal("Black", result.ActivityStatus);
    }
}
