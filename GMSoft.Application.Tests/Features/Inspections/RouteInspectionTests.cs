using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Inspections;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Inspections;

public class RouteInspectionTests
{
    private static readonly Guid Truck = Guid.NewGuid();
    private static readonly Guid Zone = Guid.NewGuid();
    private static readonly DateTime Wed7 = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    private static Customer Weekly(DateTime? confirmed = null) => new()
    {
        Id = Guid.NewGuid(), VehicleId = Truck, ZoneId = Zone, VisitDays = [3],
        CreatedAt = Wed7.AddDays(-200), PurchaseConfirmedAt = confirmed,
    };

    private static RouteDeparture[] ThreeWeeks =>
        [new(Truck, Zone, [3], Wed7.AddDays(7)), new(Truck, Zone, [3], Wed7.AddDays(14)), new(Truck, Zone, [3], Wed7.AddDays(21))];

    [Fact]
    public void A_confirmation_counts_like_a_purchase()
    {
        // Compró el 7; el controlador confirmó el 21 que sigue comprando: solo queda la semana del 28.
        var missed = CustomerActivityPolicy.MissedWeeks(Weekly(Wed7.AddDays(14)), Wed7, ThreeWeeks);
        Assert.Equal(1, missed);
    }

    [Fact]
    public void A_confirmation_older_than_the_last_purchase_changes_nothing()
    {
        var missed = CustomerActivityPolicy.MissedWeeks(Weekly(Wed7.AddDays(-30)), Wed7, ThreeWeeks);
        Assert.Equal(3, missed);
    }

    [Fact]
    public void A_confirmation_covers_a_customer_with_no_purchases_since_the_migration()
    {
        var missed = CustomerActivityPolicy.MissedWeeks(Weekly(Wed7.AddDays(21)), null, ThreeWeeks);
        Assert.Equal(0, missed);
    }

    private static readonly SaveCustomerInspectionValidator Validator = new();

    private static SaveCustomerInspectionCommand Command(bool stillBuys = true, NotBuyingReason? reason = null,
        params InspectionCountInput[] counts) => new(Guid.NewGuid(), Guid.NewGuid(), counts, stillBuys, reason, null);

    [Fact]
    public void Not_buying_needs_a_reason()
    {
        Assert.False(Validator.Validate(Command(stillBuys: false)).IsValid);
        Assert.True(Validator.Validate(Command(stillBuys: false, reason: NotBuyingReason.BuysElsewhere)).IsValid);
    }

    [Fact]
    public void Counts_cannot_be_negative_or_repeat_a_product()
    {
        var product = Guid.NewGuid();
        Assert.False(Validator.Validate(Command(true, null, new InspectionCountInput(product, -1))).IsValid);
        Assert.False(Validator.Validate(Command(true, null, new InspectionCountInput(product, 1), new InspectionCountInput(product, 2))).IsValid);
        Assert.True(Validator.Validate(Command(true, null, new InspectionCountInput(product, 0))).IsValid);
    }
}
