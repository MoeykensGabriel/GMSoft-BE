using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.ActivitySettings;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class CustomerActivitySettingsTests(PostgresFixture fixture)
{
    private static CustomerActivityPolicy Policy(Scenario f)
    {
        using var scope = f.Scope();
        return scope.ServiceProvider.GetRequiredService<CustomerActivityPolicy>();
    }

    [Fact, Trait("Grupo", "Actividad")]
    public async Task Admin_changes_thresholds_and_the_next_request_uses_them()
    {
        var f = await new Scenario(fixture).Seed(false);
        try
        {
            var defaults = await f.Send(new GetCustomerActivitySettingsQuery(), true);
            Assert.True(defaults.IsDefault);
            Assert.Equal((2, 4), (defaults.RedAfterMissedWeeks, defaults.BlackAfterMissedWeeks));
            Assert.Equal("Red", Policy(f).GetStatus(2));

            await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new UpdateCustomerActivitySettingsCommand(3, 6)));
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(new UpdateCustomerActivitySettingsCommand(4, 4), true));
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(new UpdateCustomerActivitySettingsCommand(0, 3), true));

            var saved = await f.Send(new UpdateCustomerActivitySettingsCommand(3, 6), true);
            Assert.False(saved.IsDefault);
            Assert.Equal(saved, await f.Send(new GetCustomerActivitySettingsQuery(), true));
            Assert.Equal("White", Policy(f).GetStatus(2));
            Assert.Equal("Red", Policy(f).GetStatus(5));
            Assert.Equal("Black", Policy(f).GetStatus(6));

            await f.Send(new UpdateCustomerActivitySettingsCommand(1, 2), true);
            Assert.Equal(1, await f.Read(db => db.Set<CustomerActivitySettings>().CountAsync()));
            Assert.Equal("Black", Policy(f).GetStatus(2));
        }
        finally
        {
            await f.Write(async db => await db.Set<CustomerActivitySettings>().ExecuteDeleteAsync());
        }
    }
}
