using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Reports.DriverControl;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class MarkedAtTests(PostgresFixture fixture)
{
    // La salida abrió hace dos horas: una marca de hace diez minutos es válida.
    private static async Task<Scenario> OpenedTwoHoursAgo(PostgresFixture fixture)
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(async db => (await db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId)).OpenedAt = DateTime.UtcNow.AddHours(-2));
        return f;
    }

    [Fact]
    public async Task Phone_time_is_saved_and_a_late_arrival_shows_as_sent_offline()
    {
        var f = await OpenedTwoHoursAgo(fixture);
        var marked = DateTime.UtcNow.AddMinutes(-10);

        await f.Send(f.Sale() with { MarkedAt = marked });

        var visit = Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId), true));
        Assert.NotNull(visit.MarkedAt);
        Assert.True(Math.Abs((visit.MarkedAt!.Value - marked).TotalMilliseconds) < 1);
        Assert.False(visit.MarkedAtDoubtful);
        Assert.True(visit.SentOffline);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        var report = await f.Send(new GetDriverControlQuery(today.AddDays(-1), today, f.DriverId), true);
        var driver = Assert.Single(report.Drivers);
        Assert.Equal(1, driver.OfflineVisits);
        Assert.True(Assert.Single(Assert.Single(driver.Sessions).Marks).PhoneTime);
    }

    [Fact]
    public async Task Visit_from_an_old_app_without_phone_time_still_works()
    {
        var f = await OpenedTwoHoursAgo(fixture);

        await f.Send(f.Sale());

        var visit = Assert.Single(await f.Send(new GetSessionDeliveriesQuery(f.SessionId), true));
        Assert.Null(visit.MarkedAt);
        Assert.False(visit.MarkedAtDoubtful);
        Assert.False(visit.SentOffline);
    }

    [Fact]
    public async Task Phone_time_in_the_future_or_before_opening_is_saved_as_doubtful()
    {
        var f = await OpenedTwoHoursAgo(fixture);
        var other = f.Customer(8);
        await f.Write(db => { db.Customers.Add(other); return Task.CompletedTask; });

        await f.Send(f.Sale() with { MarkedAt = DateTime.UtcNow.AddHours(1) });
        await f.Send(new RegisterDeliveryCommand(other.Id, null, DeliveryType.NoPurchase, [], [], [], null, null,
            Guid.NewGuid(), NoPurchaseReason.NotHome) { MarkedAt = DateTime.UtcNow.AddDays(-1) });

        var visits = await f.Send(new GetSessionDeliveriesQuery(f.SessionId), true);
        Assert.Equal(2, visits.Count);
        Assert.All(visits, v => { Assert.NotNull(v.MarkedAt); Assert.True(v.MarkedAtDoubtful); Assert.False(v.SentOffline); });
    }

    [Fact]
    public async Task Driver_control_is_admin_only()
    {
        var f = await OpenedTwoHoursAgo(fixture);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new GetDriverControlQuery(today, today)));
    }
}
