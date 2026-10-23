using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Application.Features.Drivers.GetList;
using GMSoft.Application.Features.Drivers.ResetPassword;
using GMSoft.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DriverVisiblePasswordTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Office_list_shows_the_last_password_set_and_the_database_keeps_it_encrypted()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var id = await f.Send(new CreateDriverCommand("Ver", "Clave", suffix, "3811234567", "ver" + suffix, "Primera123", null, null), true);

        Assert.Equal("Primera123", await Listed(f, suffix));
        var guardada = await f.Read(db => db.Drivers.Where(d => d.Id == id).Select(d => d.PasswordCipher).SingleAsync());
        Assert.NotNull(guardada);
        Assert.DoesNotContain("Primera123", guardada);

        using (var scope = f.Scope(admin: true))
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ResetDriverPasswordCommand(id, "Segunda123"));
        Assert.Equal("Segunda123", await Listed(f, suffix));

        // Una contraseña puesta antes de que existiera esto, o cifrada con otra clave, se ve vacía.
        await f.Write(async db => (await db.Drivers.FindAsync(id))!.PasswordCipher = "no-es-valido");
        Assert.Null(await Listed(f, suffix));
        using var vault = f.Scope();
        Assert.Null(vault.ServiceProvider.GetRequiredService<IPasswordVault>().Reveal(null));
    }

    private static async Task<string?> Listed(Scenario f, string document)
        => (await f.Send(new GetDriversQuery(Search: document), true)).Items.Single().Password;
}
