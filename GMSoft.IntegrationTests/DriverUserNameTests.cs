using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Features.Auth.Login;
using GMSoft.Application.Features.Drivers.ChangeUserName;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Application.Features.Drivers.GetById;
using GMSoft.Data.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DriverUserNameTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Choferes")]
    public async Task Admin_changes_the_user_name_and_the_driver_enters_with_the_new_one()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var anterior = "viejo" + suffix;
        var nuevo = "nuevo" + suffix;
        var driverId = await f.Send(new CreateDriverCommand("Usuario", "Prueba", suffix, "3811234567",
            anterior, "Clave12345", null, null), true);
        var login = await f.Send(new LoginCommand(anterior, "Clave12345"));
        var sello = await Stamp(login.UserId);

        await AsAdmin(f, new ChangeDriverUserNameCommand(driverId, nuevo));

        // Entra con el nuevo, con la misma contraseña y la misma cuenta; el viejo ya no sirve.
        await Assert.ThrowsAsync<UnauthorizedException>(() => f.Send(new LoginCommand(anterior, "Clave12345")));
        var otraVez = await f.Send(new LoginCommand(nuevo, "Clave12345"));
        Assert.Equal(login.UserId, otraVez.UserId);
        Assert.Equal(driverId, otraVez.DriverId);
        var ficha = await f.Send(new GetDriverByIdQuery(driverId), true);
        Assert.Equal(nuevo, ficha.UserName);
        // La ficha trae la contraseña para armar el mensaje completo al chofer.
        Assert.Equal("Clave12345", ficha.Password);

        // La sesión que tenía abierta queda cerrada.
        using var scope = fixture.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        Assert.False(await identity.IsTokenValidAsync(login.UserId, sello));
    }

    [Fact, Trait("Grupo", "Choferes")]
    public async Task A_user_name_in_use_or_with_odd_characters_is_rejected()
    {
        var f = await new Scenario(fixture).Seed(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var ocupado = "ocupado" + suffix;
        await f.Send(new CreateDriverCommand("Ya", "Existe", suffix, "3811234567", ocupado, "Clave12345", null, null), true);
        var otroSufijo = Guid.NewGuid().ToString("N")[..12];
        var driverId = await f.Send(new CreateDriverCommand("Quiere", "Cambiar", otroSufijo, "3811234567",
            "quiere" + otroSufijo, "Clave12345", null, null), true);

        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin(f, new ChangeDriverUserNameCommand(driverId, ocupado)));
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin(f, new ChangeDriverUserNameCommand(driverId, "con espacio")));
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin(f, new ChangeDriverUserNameCommand(driverId, " " + ocupado)));
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin(f, new ChangeDriverUserNameCommand(driverId, "")));

        // Nada cambió.
        Assert.Equal("quiere" + otroSufijo, (await f.Send(new GetDriverByIdQuery(driverId), true)).UserName);
    }

    private static async Task AsAdmin(Scenario f, IRequest command)
    {
        using var scope = f.Scope(admin: true);
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<string?> Stamp(Guid userId)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByIdAsync(userId.ToString()))!.SecurityStamp;
    }
}
