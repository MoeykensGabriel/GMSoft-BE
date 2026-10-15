using GMSoft.Application.Features.Vehicles.Schedule;

namespace GMSoft.Application.Tests;

public class VehicleScheduleTests
{
    private static readonly Guid Zone = Guid.NewGuid();
    [Fact]
    public void Empty_history_is_seven_empty_days() => Assert.All(ScheduleRules.Suggest([]), d => Assert.Null(d.ZoneId));

    [Fact]
    public void History_counts_route_days_and_breaks_ties_by_recency()
    {
        var other = Guid.NewGuid();
        var history = new[] {
            new ScheduleHistory(Zone, "Centro", new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), [1, 4]),
            new ScheduleHistory(other, "Norte", new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), [1]) };
        var result = ScheduleRules.Suggest(history);
        Assert.Equal(other, result[0].ZoneId);
        Assert.Equal(Zone, result[3].ZoneId);
        Assert.Null(result[1].ZoneId);
    }
    [Fact]
    public void Historical_day_falls_back_to_Argentina_not_UTC()
    {
        var result = ScheduleRules.Suggest([new(Zone, "Centro", new(2026, 10, 13, 1, 0, 0, DateTimeKind.Utc), null)]);
        Assert.Equal(Zone, result[0].ZoneId);
        Assert.Null(result[1].ZoneId);
    }
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    public void Coverage_checks_even_nonworking_days(int day, bool expected)
    {
        var result = ScheduleRules.Coverage([new(1, Zone, "Centro"), new(2, null, null)], Zone, [day]);
        Assert.Equal(expected, result.Covered);
        Assert.Equal(expected, result.Days.Single().Matches);
    }
    [Fact]
    public void Missing_schedule_means_unknown()
    {
        var result = ScheduleRules.Coverage([], Zone, [1, 4]);
        Assert.Null(result.Covered);
        Assert.All(result.Days, d => Assert.Null(d.Matches));
    }
    [Fact]
    public void Partial_coverage_is_not_covered()
    {
        var result = ScheduleRules.Coverage([new(1, Zone, "Centro"), new(4, null, null)], Zone, [4, 1]);
        Assert.False(result.Covered);
        Assert.Equal(new[] { 1, 4 }, result.Days.Select(d => d.Day));
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("eight")]
    [InlineData("emptyZone")]
    [InlineData("nullEntry")]
    [InlineData("nullArray")]
    public void Save_requires_exactly_seven_valid_days(string invalid)
    {
        var days = Enumerable.Range(1, 7).Select(d => new ScheduleDayInput(d, null)).ToArray();
        switch (invalid)
        {
            case "missing": days = days[..6]; break;
            case "duplicate": days[6] = new(1, null); break;
            case "zero": days[0] = new(0, null); break;
            case "eight": days[0] = new(8, null); break;
            case "emptyZone": days[0] = new(1, Guid.Empty); break;
            case "nullEntry": days[0] = null!; break;
            case "nullArray": days = null!; break;
        }
        Assert.False(new SaveVehicleScheduleValidator().Validate(new SaveVehicleScheduleCommand(Guid.NewGuid(), days)).IsValid);
    }
    [Fact]
    public void Entire_week_off_is_a_valid_configuration() => Assert.True(new SaveVehicleScheduleValidator()
        .Validate(new SaveVehicleScheduleCommand(Guid.NewGuid(), Enumerable.Range(1, 7).Select(d => new ScheduleDayInput(d, null)).ToArray())).IsValid);
}

