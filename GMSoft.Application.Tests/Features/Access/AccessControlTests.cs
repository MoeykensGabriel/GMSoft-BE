using GMSoft.Application.Features.Access;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Access;

public class AccessControlTests
{
    private static readonly Guid Driver = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PhoneA = Guid.NewGuid();
    private static readonly Guid PhoneB = Guid.NewGuid();

    private static AccessEventRow Row(int minutes, AccessEventKind kind, AccessEndReason? reason = null, Guid? device = null)
        => new(Driver, "Gamez", Day.AddMinutes(minutes), kind, reason, AccessClient.Browser, "Android 14 · Chrome 130", device);

    [Fact]
    public void A_login_after_a_recorded_exit_is_normal()
    {
        var day = AccessControlAnalysis.Build([
            Row(-600, AccessEventKind.Login, device: PhoneA),
            Row(-60, AccessEventKind.Logout, AccessEndReason.ReceptionCompleted),
            Row(300, AccessEventKind.Login, device: PhoneA),
        ], Day).Single();

        // El evento de ayer solo sirve de contexto: no se muestra.
        Assert.Single(day.Events);
        Assert.Equal(AccessLoss.None, day.Events[0].Loss);
        Assert.Equal(0, day.Problems);
    }

    [Fact]
    public void A_login_without_an_exit_tells_whether_the_phone_lost_its_data()
    {
        var day = AccessControlAnalysis.Build([
            Row(300, AccessEventKind.Login, device: PhoneA),
            Row(400, AccessEventKind.Login, device: PhoneA),
            Row(500, AccessEventKind.Login, device: PhoneB),
            Row(600, AccessEventKind.Rejected, AccessEndReason.Expired),
            Row(601, AccessEventKind.Login, device: PhoneB),
        ], Day).Single();

        Assert.Equal([AccessLoss.None, AccessLoss.NoExitRecorded, AccessLoss.DataCleared, AccessLoss.None, AccessLoss.None],
            day.Events.Select(e => e.Loss));
        Assert.Equal(4, day.Logins);
        Assert.Equal(3, day.Problems);
    }

    [Fact]
    public void Office_decisions_are_not_problems()
    {
        var day = AccessControlAnalysis.Build([
            Row(300, AccessEventKind.Login, device: PhoneA),
            Row(310, AccessEventKind.Rejected, AccessEndReason.CredentialsChanged),
            Row(320, AccessEventKind.Login, device: PhoneA),
            Row(330, AccessEventKind.Logout, AccessEndReason.Manual),
        ], Day).Single();
        Assert.Equal(0, day.Problems);
    }

    [Fact]
    public void A_driver_with_only_old_events_is_left_out()
        => Assert.Empty(AccessControlAnalysis.Build([Row(-30, AccessEventKind.Login, device: PhoneA)], Day));

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-A145M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36",
        true, "Android 14 · Chrome 130", AccessClient.InstalledApp)]
    [InlineData("Mozilla/5.0 (Linux; Android 13; moto g32 Build/T1SS; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/129.0.0.0 Mobile Safari/537.36 WhatsApp/2.24",
        false, "Android 13 · dentro de WhatsApp", AccessClient.EmbeddedBrowser)]
    [InlineData("Mozilla/5.0 (Linux; Android 12; wv) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36",
        null, "Android 12 · vista web de otra app", AccessClient.EmbeddedBrowser)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",
        false, "iPhone · Safari", AccessClient.Browser)]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SAMSUNG SM-A546E) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/25.0 Chrome/121.0.0.0 Mobile Safari/537.36",
        false, "Android 14 · Samsung Internet 25", AccessClient.Browser)]
    public void The_user_agent_is_summarized_for_the_office(string userAgent, bool? installed, string device, AccessClient client)
    {
        Assert.Equal(device, AccessDevice.Describe(userAgent));
        Assert.Equal(client, AccessDevice.Client(userAgent, installed));
    }

    [Fact]
    public void The_app_cannot_report_reasons_that_belong_to_the_server()
    {
        var validator = new LogoutCommandValidator();
        Assert.True(validator.Validate(new LogoutCommand(AccessEndReason.Manual)).IsValid);
        Assert.False(validator.Validate(new LogoutCommand(AccessEndReason.Expired)).IsValid);
        Assert.False(validator.Validate(new LogoutCommand((AccessEndReason)99)).IsValid);
    }
}
