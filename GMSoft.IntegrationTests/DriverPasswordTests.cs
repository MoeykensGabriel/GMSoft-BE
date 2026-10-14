using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Application.Features.Drivers.ResetPassword;
using GMSoft.Data.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DriverPasswordTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Choferes")]
    public async Task Admin_sets_a_new_password_that_follows_the_rules()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var userName = "clave" + suffix;
        var driverId = await f.Send(new CreateDriverCommand("Clave", "Prueba", suffix, "3811234567",
            userName, "Inicial1234", null, null), true);

        await Assert.ThrowsAsync<ValidationException>(() =>
            AsAdmin(f, new ResetDriverPasswordCommand(driverId, "corta")));
        Assert.True(await CheckPassword(userName, "Inicial1234"));

        await AsAdmin(f, new ResetDriverPasswordCommand(driverId, "Nueva12345"));

        Assert.True(await CheckPassword(userName, "Nueva12345"));
        Assert.False(await CheckPassword(userName, "Inicial1234"));
    }

    private static async Task AsAdmin(Scenario f, IRequest command)
    {
        using var scope = f.Scope(admin: true);
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<bool> CheckPassword(string userName, string password)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync(userName);
        return user is not null && await users.CheckPasswordAsync(user, password);
    }
}
