using GMSoft.Application.Common;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class CustomerActivityTests
{
    private static readonly Guid Truck = Guid.NewGuid();
    private static readonly Guid Zone = Guid.NewGuid();

    // Miercoles 7 de octubre de 2026, 12:00 en Argentina.
    private static readonly DateTime Wed7 = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    private static Customer Weekly(params int[] visitDays) => new()
    {
        Id = Guid.NewGuid(), VehicleId = Truck, ZoneId = Zone, VisitDays = visitDays,
        CreatedAt = Wed7.AddDays(-200)
    };

    private static RouteDeparture Departure(DateTime openedAt, params int[] days)
        => new(Truck, Zone, days, openedAt);

    [Fact]
    public void Each_week_the_truck_went_without_a_sale_is_one_missed_turn()
    {
        var customer = Weekly(3);
        // Compro el miercoles 7. El camion volvio a pasar el 14 y el 21 y no compro.
        var missed = CustomerActivityPolicy.MissedWeeks(customer, Wed7,
            [Departure(Wed7, 3), Departure(Wed7.AddDays(7), 3), Departure(Wed7.AddDays(14), 3)]);
        Assert.Equal(2, missed);
    }

    [Fact]
    public void A_week_without_a_departure_does_not_count_against_the_customer()
    {
        var customer = Weekly(3);
        // Pasaron tres semanas, pero el camion solo salio en una.
        var missed = CustomerActivityPolicy.MissedWeeks(customer, Wed7, [Departure(Wed7.AddDays(21), 3)]);
        Assert.Equal(1, missed);
    }

    [Fact]
    public void Calendar_days_alone_never_make_a_customer_inactive()
        => Assert.Equal(0, CustomerActivityPolicy.MissedWeeks(Weekly(3), Wed7.AddDays(-90), []));

    [Fact]
    public void Two_departures_in_the_same_week_are_a_single_turn()
    {
        var customer = Weekly(1, 4);
        var monday = Wed7.AddDays(5);
        var missed = CustomerActivityPolicy.MissedWeeks(customer, Wed7,
            [Departure(monday, 1), Departure(monday.AddDays(3), 4)]);
        Assert.Equal(1, missed);
    }

    [Fact]
    public void Buying_later_in_the_same_week_covers_an_earlier_missed_visit()
    {
        var customer = Weekly(1, 4);
        var monday = Wed7.AddDays(5);
        // No compro el lunes, compro el jueves: la semana esta cubierta.
        var missed = CustomerActivityPolicy.MissedWeeks(customer, monday.AddDays(3),
            [Departure(monday, 1), Departure(monday.AddDays(3), 4)]);
        Assert.Equal(0, missed);
    }

    [Fact]
    public void Only_departures_of_his_truck_zone_and_days_count()
    {
        var customer = Weekly(3);
        var nextWeek = Wed7.AddDays(7);
        var missed = CustomerActivityPolicy.MissedWeeks(customer, Wed7,
        [
            new(Guid.NewGuid(), Zone, [3], nextWeek),
            new(Truck, Guid.NewGuid(), [3], nextWeek),
            Departure(nextWeek, 5),
        ]);
        Assert.Equal(0, missed);
    }

    [Fact]
    public void Never_purchased_counts_turns_since_creation_without_inventing_a_purchase()
    {
        var customer = Weekly(3);
        customer.CreatedAt = Wed7;
        var missed = CustomerActivityPolicy.MissedWeeks(customer, null,
            [Departure(Wed7.AddDays(-7), 3), Departure(Wed7.AddHours(2), 3), Departure(Wed7.AddDays(7), 3)]);
        Assert.Equal(2, missed);

        var dto = CustomerMapping.ToDto(customer, null, missed, new(2, 4), Wed7.AddDays(8));
        Assert.Equal("Red", dto.ActivityStatus);
        Assert.Null(dto.DaysWithoutPurchase);
        Assert.True(dto.SinComprasRegistradas);
    }

    [Fact]
    public void Customer_outside_any_route_has_no_turns_to_miss()
    {
        var sinCamion = Weekly(3);
        sinCamion.VehicleId = null;
        var sinDias = Weekly();
        var departures = new[] { Departure(Wed7.AddDays(7), 3) };
        Assert.Equal(0, CustomerActivityPolicy.MissedWeeks(sinCamion, Wed7, departures));
        Assert.Equal(0, CustomerActivityPolicy.MissedWeeks(sinDias, Wed7, departures));
    }

    [Theory]
    [InlineData(0, "White")]
    [InlineData(1, "White")]
    [InlineData(2, "Red")]
    [InlineData(3, "Red")]
    [InlineData(4, "Black")]
    [InlineData(20, "Black")]
    public void Missed_turns_use_inclusive_thresholds(int missed, string expected)
    {
        var dto = CustomerMapping.ToDto(Weekly(3), Wed7, missed, new(2, 4), Wed7.AddDays(10));
        Assert.Equal(expected, dto.ActivityStatus);
        Assert.Equal(missed, dto.WeeksWithoutPurchase);
        // Los dias corridos siguen informandose, pero ya no deciden el color.
        Assert.Equal(10, dto.DaysWithoutPurchase);
    }

    [Fact]
    public void Thresholds_can_change_without_changing_purchase_history()
        => Assert.Equal("White", CustomerMapping.ToDto(Weekly(3), Wed7, 3, new(5, 8), Wed7).ActivityStatus);

    [Fact]
    public void Week_changes_at_Argentina_midnight_between_sunday_and_monday()
    {
        // Lunes 5 de octubre 00:00 en Argentina es 03:00 UTC.
        var mondayStart = new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 9, 28), BusinessTime.WeekStart(mondayStart.AddSeconds(-1)));
        Assert.Equal(new DateOnly(2026, 10, 5), BusinessTime.WeekStart(mondayStart));
        Assert.Equal(new DateOnly(2026, 10, 5), BusinessTime.WeekStart(mondayStart.AddDays(6)));
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
    [InlineData(0, 4)]
    [InlineData(-1, 4)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    public void Invalid_thresholds_are_rejected(int red, int black)
        => Assert.Throws<ArgumentException>(() => new CustomerActivityPolicy(red, black));
}
