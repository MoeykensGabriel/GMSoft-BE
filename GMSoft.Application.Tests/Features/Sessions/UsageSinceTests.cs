using GMSoft.Application.Features.Sessions.UsageSince;

namespace GMSoft.Application.Tests.Features.Sessions;

public class UsageSinceTests
{
    [Fact]
    public void The_day_of_the_first_trip_is_day_one()
    {
        var first = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new UsageSinceDto(new(2026, 10, 24), 1), UsageSince.From(first, first.AddHours(5)));
        Assert.Equal(new UsageSinceDto(new(2026, 10, 24), 12), UsageSince.From(first, new(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Days_change_at_midnight_in_Argentina_not_in_UTC()
    {
        // 01:00 UTC del 25 son las 22:00 del 24 en Argentina.
        var first = new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc);
        var result = UsageSince.From(first, new DateTime(2026, 10, 25, 2, 59, 0, DateTimeKind.Utc));
        Assert.Equal(new DateOnly(2026, 10, 24), result.Since);
        Assert.Equal(1, result.Days);
        Assert.Equal(2, UsageSince.From(first, new DateTime(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc)).Days);
    }

    [Fact]
    public void Without_trips_there_is_no_date()
        => Assert.Equal(new UsageSinceDto(null, null), UsageSince.From(null, DateTime.UtcNow));
}
