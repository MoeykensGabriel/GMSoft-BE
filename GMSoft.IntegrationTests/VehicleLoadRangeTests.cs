using System.Reflection;
using GMSoft.API.Controllers;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.VehicleLoads.Ranges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleLoadRangeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Guardar_y_leer_respeta_limites_opcionales_y_orden_del_catalogo()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        Assert.Empty(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true));
        // El dispenser iría primero alfabéticamente: debe quedar último igual.
        await f.Write(async db => (await db.Products.FindAsync(f.UnitId))!.Detail = "A Dispenser");
        await f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId,
            [new(f.UnitId, 1, 4), new(f.ProductId, 20, null), new(f.NoneId, null, 100000)]), true);
        var rows = await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true);
        Assert.Equal(new[] {
            new VehicleLoadRangeDto(f.NoneId, null, 100000),
            new VehicleLoadRangeDto(f.ProductId, 20, null),
            new VehicleLoadRangeDto(f.UnitId, 1, 4)
        }, rows);
        // Los 20 dispensers superan el máximo: el rango solo avisa en el frontend.
        await f.Load();
    }

    [Fact]
    public async Task Reemplazar_borra_omitidos_y_lista_vacia_borra_todo_solo_del_vehiculo()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        var other = await new Scenario(fixture).Seed(open: false);
        await other.Send(new SaveVehicleLoadRangesCommand(other.VehicleId, [new(other.ProductId, 0, 0)]), true);
        await f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId,
            [new(f.ProductId, 1, 4), new(f.NoneId, null, 10)]), true);
        await f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId, [new(f.ProductId, 2, 3)]), true);
        Assert.Equal(new VehicleLoadRangeDto(f.ProductId, 2, 3),
            Assert.Single(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true)));
        Assert.Equal(1, await f.Read(db => db.VehicleLoadRanges.CountAsync(r => r.VehicleId == f.VehicleId)));
        await f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId, []), true);
        Assert.Empty(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true));
        Assert.Equal(0, await f.Read(db => db.VehicleLoadRanges.CountAsync(r => r.VehicleId == f.VehicleId)));
        Assert.Single(await other.Send(new GetVehicleLoadRangesQuery(other.VehicleId), true));
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(null, null)]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    [InlineData(100001, null)]
    [InlineData(null, 100001)]
    public async Task Limites_invalidos_no_se_guardan(int? min, int? max)
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(
            new SaveVehicleLoadRangesCommand(f.VehicleId, [new(f.ProductId, min, max)]), true));
        Assert.Empty(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true));
    }

    [Fact]
    public async Task Repetidos_lista_nula_fila_nula_y_vehiculo_vacio_son_invalidos()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(new SaveVehicleLoadRangesCommand(
            f.VehicleId, [new(f.ProductId, 0, null), new(f.ProductId, null, 10)]), true));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId, null!), true));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId, [null!]), true));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(new SaveVehicleLoadRangesCommand(Guid.Empty, []), true));
    }

    [Fact]
    public async Task Producto_inexistente_no_borra_los_rangos_previos_y_vehiculo_inexistente_es_not_found()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await f.Send(new SaveVehicleLoadRangesCommand(f.VehicleId, [new(f.ProductId, 0, 100000)]), true);
        var missing = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(
            new SaveVehicleLoadRangesCommand(f.VehicleId, [new(missing, 1, 4)]), true));
        Assert.Contains(missing.ToString(), error.Message);
        Assert.Equal(new VehicleLoadRangeDto(f.ProductId, 0, 100000),
            Assert.Single(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true)));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Send(new GetVehicleLoadRangesQuery(missing), true));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Send(new SaveVehicleLoadRangesCommand(missing, []), true));
    }

    [Fact]
    public async Task Chofer_no_puede_leer_ni_guardar()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new GetVehicleLoadRangesQuery(f.VehicleId)));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(
            new SaveVehicleLoadRangesCommand(f.VehicleId, [new(f.ProductId, 1, 4)])));
        Assert.Empty(await f.Send(new GetVehicleLoadRangesQuery(f.VehicleId), true));
    }

    [Fact]
    public void Endpoints_son_solo_admin_y_tienen_las_rutas_esperadas()
    {
        var get = typeof(VehiclesController).GetMethod(nameof(VehiclesController.GetLoadRanges))!;
        var put = typeof(VehiclesController).GetMethod(nameof(VehiclesController.SaveLoadRanges))!;
        Assert.Equal("Admin", get.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("Admin", put.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("{id:guid}/load-ranges", get.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{id:guid}/load-ranges", put.GetCustomAttribute<HttpPutAttribute>()!.Template);
    }
}
