using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Reports.DriverControl;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Reports;

public class DriverControlAnalysisTests
{
    static readonly DateTime Open = new(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc); // 08:00 en Argentina

    static DriverControlSessionRow Session(Guid? id = null, Guid? driver = null, DateTime? opened = null) =>
        new(id ?? Guid.NewGuid(), driver ?? Guid.Empty, "Juan Prueba", "DE", "AA000AA", "Centro",
            opened ?? Open, null, SessionStatus.Open, false, null);

    static DriverControlVisitRow Visit(Guid session, DateTime delivered, DateTime? marked = null, bool doubtful = false,
        DeliveryType type = DeliveryType.Sale, NoPurchaseReason? reason = null) =>
        new(session, "Cliente", type, reason, delivered, marked, doubtful, false);

    [Fact]
    public void Five_marks_a_few_seconds_apart_with_phone_time_are_a_burst()
    {
        var s = Session();
        var visits = Enumerable.Range(0, 6).Select(i => Visit(s.Id, Open.AddHours(3).AddSeconds(i * 6), Open.AddHours(3).AddSeconds(i * 6)))
            .Append(Visit(s.Id, Open.AddHours(1), Open.AddHours(1))).ToList();

        var result = DriverControlAnalysis.Session(s, visits);

        var burst = Assert.Single(result.Bursts);
        Assert.Equal(6, burst.Count);
        Assert.False(burst.ServerTime);
        Assert.Equal(6, result.Marks.Count(m => m.InBurst));
        Assert.False(result.Marks[0].InBurst);
    }

    [Fact]
    public void Marks_sent_together_without_signal_are_not_a_burst_when_the_phone_times_are_spread()
    {
        var s = Session();
        var arrival = Open.AddHours(4);
        // Llegaron todas juntas al servidor, pero se tocaron cada 4 minutos.
        var visits = Enumerable.Range(0, 8).Select(i => Visit(s.Id, arrival.AddSeconds(i), Open.AddHours(2).AddMinutes(i * 4))).ToList();

        var result = DriverControlAnalysis.Session(s, visits);

        Assert.Empty(result.Bursts);
        Assert.True(result.OfflineVisits >= 6);
        Assert.Equal(240, result.MedianIntervalSeconds);
    }

    [Fact]
    public void Old_visits_without_phone_time_mark_the_burst_as_server_time()
    {
        var s = Session();
        var visits = Enumerable.Range(0, 5).Select(i => Visit(s.Id, Open.AddHours(2).AddSeconds(i * 10))).ToList();

        var burst = Assert.Single(DriverControlAnalysis.Session(s, visits).Bursts);

        Assert.True(burst.ServerTime);
    }

    [Fact]
    public void Doubtful_phone_time_falls_back_to_server_time()
    {
        var s = Session();
        var delivered = Open.AddHours(2);
        var mark = Assert.Single(DriverControlAnalysis.Session(s, [Visit(s.Id, delivered, delivered.AddHours(5), doubtful: true)]).Marks);

        Assert.Equal(delivered, mark.At);
        Assert.False(mark.PhoneTime);
        Assert.True(mark.Doubtful);
        Assert.False(mark.Offline);
    }

    [Fact]
    public void Driver_totals_add_up_and_usual_opening_is_in_argentina_time()
    {
        var driver = Guid.NewGuid();
        var a = Session(driver: driver);
        var b = Session(driver: driver, opened: Open.AddDays(1).AddHours(2));
        var c = Session(driver: driver, opened: Open.AddDays(2).AddMinutes(30));
        var visits = new[]
        {
            Visit(a.Id, Open.AddHours(1), type: DeliveryType.NoPurchase, reason: NoPurchaseReason.DidNotWant),
            Visit(b.Id, Open.AddDays(1).AddHours(3), type: DeliveryType.NoPurchase, reason: NoPurchaseReason.NotHome),
        };

        var row = Assert.Single(DriverControlAnalysis.Build([a, b, c], visits));

        Assert.Equal(3, row.SessionCount);
        Assert.Equal(2, row.NoPurchase);
        Assert.Equal(1, row.DidNotWant);
        Assert.Equal(new TimeOnly(8, 30), row.UsualOpening);
    }

    [Theory]
    [InlineData(-10, false)]   // marcada 10 min antes de llegar: normal
    [InlineData(1, false)]     // reloj del celular 1 min adelantado: tolerado
    [InlineData(10, true)]     // en el futuro
    [InlineData(-600, true)]   // antes de abrir la salida
    public void Phone_time_is_doubtful_in_the_future_or_before_opening(int minutesFromArrival, bool doubtful)
    {
        var arrival = Open.AddHours(3);
        var (marked, result) = DeliveryMark.Evaluate(arrival.AddMinutes(minutesFromArrival), arrival, Open);

        Assert.Equal(doubtful, result);
        Assert.Equal(DateTimeKind.Utc, marked!.Value.Kind);
    }

    [Fact]
    public void Missing_phone_time_is_not_doubtful_nor_offline()
    {
        Assert.Equal((null, false), DeliveryMark.Evaluate(null, Open, Open));
        Assert.False(DeliveryMark.SentOffline(null, false, Open));
        Assert.True(DeliveryMark.SentOffline(Open.AddMinutes(-6), false, Open));
        Assert.False(DeliveryMark.SentOffline(Open.AddMinutes(-4), false, Open));
    }
}
