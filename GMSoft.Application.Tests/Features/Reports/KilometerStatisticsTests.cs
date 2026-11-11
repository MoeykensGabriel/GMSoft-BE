using GMSoft.Application.Features.Reports.VehicleKilometers;

namespace GMSoft.Application.Tests.Features.Reports;

public class KilometerStatisticsTests
{
    private static readonly Guid Truck = Guid.NewGuid();
    // 12:00 UTC = 09:00 en Argentina: cada salida cae en el dia de negocio que dice.
    private static readonly DateTime Day1 = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static SessionKilometersRow Trip(DateTime opened, int kmOpen, int kmClose, params string[] zones) =>
        new(Truck, "F100", "AVP463", opened, kmOpen, kmClose, zones.Length == 0 ? ["SMT"] : zones);

    [Fact]
    public void Odd_count_uses_the_middle_value_and_even_count_averages_the_two_middle_ones()
    {
        Assert.Equal(40m, KilometerStatistics.Median([60, 40, 10]));
        Assert.Equal(25m, KilometerStatistics.Median([10, 40, 20, 30]));
        Assert.Equal(20.5m, KilometerStatistics.Median([20, 21]));
        Assert.Null(KilometerStatistics.Median([]));
    }

    [Fact]
    public void Averages_round_to_one_decimal_away_from_zero()
    {
        // 10 + 10 + 11 = 31 / 3 = 10.333 -> 10.3; dia: 31 / 2 dias = 15.5
        var v = Assert.Single(KilometerStatistics.Build([
            Trip(Day1, 0, 10), Trip(Day1.AddHours(3), 10, 20), Trip(Day1.AddDays(1), 20, 31)]));
        Assert.Equal(10.3m, v.AveragePerSession);
        Assert.Equal(2, v.DaysWorked);
        Assert.Equal(15.5m, v.AveragePerDay);
        Assert.Equal(31, v.TotalKilometers);
    }

    [Fact]
    public void An_invented_reading_moves_the_average_but_not_the_median()
    {
        var v = Assert.Single(KilometerStatistics.Build([
            Trip(Day1, 1000, 1040), Trip(Day1.AddDays(1), 1040, 1080),
            Trip(Day1.AddDays(2), 1080, 301080), Trip(Day1.AddDays(3), 301080, 301120)]));
        Assert.Equal(40m, v.MedianPerSession);
        Assert.Equal(75030m, v.AveragePerSession);
        Assert.Equal(40, v.MinPerSession);
        Assert.Equal(300000, v.MaxPerSession);
    }

    [Fact]
    public void Counts_zero_kilometer_trips_and_kilometers_between_trips()
    {
        // Volvio con 1050 y salio al otro dia con 1070: 20 km fuera de reparto.
        var v = Assert.Single(KilometerStatistics.Build([
            Trip(Day1.AddDays(1), 1070, 1070), Trip(Day1, 1000, 1050), Trip(Day1.AddDays(2), 1075, 1100)]));
        Assert.Equal(1, v.ZeroKilometerSessions);
        Assert.Equal(25, v.KilometersBetweenSessions);
        Assert.Equal(3, v.Sessions);
    }

    [Fact]
    public void Routes_group_by_zones_in_order_and_most_frequent_first()
    {
        var v = Assert.Single(KilometerStatistics.Build([
            Trip(Day1, 0, 30, "SMT", "Tafi Viejo"),
            Trip(Day1.AddDays(1), 30, 40, "Tafi Viejo", "SMT"),
            Trip(Day1.AddDays(2), 40, 75, "SMT", "Tafi Viejo"),
            Trip(Day1.AddDays(3), 75, 100, "SMT", "Tafi Viejo")]));
        Assert.Collection(v.Routes,
            r =>
            {
                Assert.Equal("SMT → Tafi Viejo", r.Route); Assert.Equal(3, r.Sessions);
                Assert.Equal(30m, r.MedianPerSession); Assert.Equal(30m, r.AveragePerSession);
                Assert.Equal(25, r.MinPerSession); Assert.Equal(35, r.MaxPerSession);
            },
            r => { Assert.Equal("Tafi Viejo → SMT", r.Route); Assert.Equal(1, r.Sessions); Assert.Equal(10m, r.AveragePerSession); });
    }

    [Fact]
    public void Vehicles_without_trips_are_left_out_and_the_rest_sorted_by_name()
    {
        var other = new SessionKilometersRow(Guid.NewGuid(), "Dfsk - DE", "AG989DE", Day1, 0, 5, ["SMT"]);
        var result = KilometerStatistics.Build([Trip(Day1, 0, 10), other]);
        Assert.Equal(["Dfsk - DE", "F100"], result.Select(v => v.VehicleName));
        Assert.Empty(KilometerStatistics.Build([]));
    }
}
