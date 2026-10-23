using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Auth.Login;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class LoginWithoutLockoutTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Many_wrong_passwords_never_lock_the_account_and_an_old_lock_is_ignored()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var userName = "sinbloqueo" + suffix;
        await f.Send(new CreateDriverCommand("Sin", "Bloqueo", suffix, "3811234567", userName, "Correcta123", null, null), true);

        for (var i = 0; i < 8; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => f.Send(new LoginCommand(userName, "Equivocada1")));
        Assert.Equal(userName, (await f.Send(new LoginCommand(userName, "Correcta123"))).UserName);

        // Una cuenta que quedó bloqueada con la versión anterior entra igual.
        using (var scope = f.Scope(true))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync(userName))!;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            await users.UpdateAsync(user);
        }
        Assert.Equal(userName, (await f.Send(new LoginCommand(userName, "Correcta123"))).UserName);
    }
}
