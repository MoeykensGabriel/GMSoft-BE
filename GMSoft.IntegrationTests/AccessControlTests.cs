using GMSoft.Application.Common;
using GMSoft.Application.Features.Access;
using GMSoft.Application.Features.Auth.Login;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class AccessControlTests(PostgresFixture fixture)
{
    private const string Android = "Mozilla/5.0 (Linux; Android 14; SM-A145M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36";

    [Fact, Trait("Grupo", "Choferes")]
    public async Task The_office_sees_every_login_exit_and_lost_session_of_the_day()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var userName = "sesiones" + suffix;
        var driverId = await f.Send(new CreateDriverCommand("Sesiones", suffix, suffix, "3811234567",
            userName, "Inicial1234", null, null), true);
        var phone = Guid.NewGuid();

        var first = await AsGuest(f, new LoginCommand(userName, "Inicial1234", Installed: true, DeviceId: phone));
        await AsDriver(f, first.UserId, driverId, new LogoutCommand(AccessEndReason.Manual));
        await AsGuest(f, new LoginCommand(userName, "Inicial1234", Installed: true, DeviceId: phone));
        // El celular borró los datos: entra de nuevo sin haber salido y con otro identificador.
        await AsGuest(f, new LoginCommand(userName, "Inicial1234", Installed: false, DeviceId: Guid.NewGuid()));
        // Varios pedidos con el mismo token vencido: se anota una sola vez.
        await f.Send(new RecordAccessRejectionCommand(first.UserId, driverId, "token-1", AccessEndReason.Expired, Android));
        await f.Send(new RecordAccessRejectionCommand(first.UserId, driverId, "token-1", AccessEndReason.Expired, Android));
        // La oficina no se controla.
        await AsDriver(f, f.AdminId, null, new LogoutCommand(AccessEndReason.Manual));

        var today = DateOnly.FromDateTime(DateTime.UtcNow + BusinessTime.Offset);
        var report = await f.Send(new GetAccessControlQuery(today, driverId), true);

        var day = Assert.Single(report.Drivers);
        Assert.Equal(
            [(AccessEventKind.Login, (AccessEndReason?)null, AccessLoss.None), (AccessEventKind.Logout, AccessEndReason.Manual, AccessLoss.None),
             (AccessEventKind.Login, null, AccessLoss.None), (AccessEventKind.Login, null, AccessLoss.DataCleared),
             (AccessEventKind.Rejected, AccessEndReason.Expired, AccessLoss.None)],
            day.Events.Select(e => (e.Kind, e.Reason, e.Loss)));
        Assert.Equal(3, day.Logins);
        Assert.Equal(2, day.Problems);
        Assert.Equal([AccessClient.InstalledApp, AccessClient.Unknown, AccessClient.InstalledApp, AccessClient.Browser, AccessClient.Unknown],
            day.Events.Select(e => e.Client));
        Assert.Equal("Android 14 · Chrome 130", day.Events[^1].Device);
        Assert.Equal(0, await f.Read(db => db.AccessEvents.CountAsync(e => e.UserId == f.AdminId)));
    }

    [Fact, Trait("Grupo", "Choferes")]
    public async Task Only_the_office_reads_the_control()
    {
        var f = await new Scenario(fixture).Seed(false);
        await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.ForbiddenException>(() =>
            f.Send(new GetAccessControlQuery(new DateOnly(2026, 11, 20))));
    }

    private static async Task<T> AsGuest<T>(Scenario f, IRequest<T> request)
    {
        using var scope = f.Scope();
        var user = scope.ServiceProvider.GetRequiredService<TestUser>();
        user.UserId = null;
        user.DriverId = null;
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private static async Task AsDriver(Scenario f, Guid userId, Guid? driverId, IRequest request)
    {
        using var scope = f.Scope();
        var user = scope.ServiceProvider.GetRequiredService<TestUser>();
        user.UserId = userId;
        user.DriverId = driverId;
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }
}
