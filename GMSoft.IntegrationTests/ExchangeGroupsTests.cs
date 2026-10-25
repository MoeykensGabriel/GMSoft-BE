using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Products.Create;
using GMSoft.Application.Features.Products.Update;
using GMSoft.Application.Features.Products.GetById;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class ExchangeGroupsTests(PostgresFixture fixture)
{
    private async Task<Scenario> Seed()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(async db =>
        {
            foreach (var id in new[] { f.ProductId, f.NoneId, f.UnitId })
            {
                var p = await db.Products.SingleAsync(p => p.Id == id);
                p.Tracking = ContainerTracking.ByBalance;
                p.ExchangeGroup = id == f.ProductId ? "Bidones" : "BIDONES";
            }
        });
        return f;
    }

    private static RegisterDeliveryCommand Exchange(Scenario f, int returned = 1) =>
        f.Sale(1) with { ContainersIn = [new(f.NoneId, returned)] };

    [Fact]
    public async Task Cambio_conserva_libro_saldo_vacios_cuenta_recepcion_y_reintento()
    {
        var f = await Seed();
        var command = Exchange(f);
        var result = await f.Send(command);
        Assert.Equal(result, await f.Send(command with { ExpectedSessionId = Guid.NewGuid() }));
        Assert.Equal(1, await f.Sales());
        Assert.Equal(1, await f.Balance());
        Assert.Equal(1, await f.Stock(ContainerState.Empty, f.NoneId));
        var movements = await f.Read(db => db.ContainerMovements.Where(m => m.DeliveryId == result.DeliveryId).ToListAsync());
        var exchange = Assert.Single(movements, m => m.Type == ContainerMovementType.ExchangeCorrection);
        Assert.Equal(1, exchange.Quantity);
        Assert.Equal("Cambio por Bidon", exchange.Notes);
        Assert.Equal(f.UserId, exchange.RegisteredByUserId);
        Assert.Equal(-1, Assert.Single(movements, m => m.Type == ContainerMovementType.ReturnedFromCustomer).Quantity);
        var balances = await f.Read(db => db.CustomerContainerBalances.Where(b => b.CustomerId == f.CustomerId).ToListAsync());
        foreach (var balance in balances)
        {
            Assert.True(balance.Quantity >= 0);
            Assert.Equal(movements.Where(m => m.ProductId == balance.ProductId).Sum(m => m.Quantity), balance.Quantity);
        }
        using var scope = f.Scope(true);
        var account = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>().GetAccountStatementAsync(f.CustomerId, 100);
        var row = Assert.Single(account.Rows);
        Assert.Equal(exchange.Notes, Assert.Single(row.ContainerExchanges).Notes);
        Assert.Equal(1, Assert.Single(row.ContainersReturned).Quantity);
        var visits = await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetDeliveriesAsync(f.SessionId);
        Assert.Equal(2, Assert.Single(visits).Containers.Count);
        var reception = await f.Send(new CloseSessionCommand(f.SessionId, 110,
            [new(f.ProductId, ContainerState.Full, 19), new(f.NoneId, ContainerState.Full, 20),
             new(f.UnitId, ContainerState.Full, 20), new(f.NoneId, ContainerState.Empty, 1)]), true);
        Assert.True(reception.CuadraTodo);
        Assert.Empty(reception.Faltante);
        Assert.Equal(result, await f.Send(command));
    }

    [Theory]
    [InlineData("exceso", new[] { "Cambio por Bidon; 1 tenía sin registrar" })]
    [InlineData("sin venta", new[] { "Tenía sin registrar" })]
    [InlineData("sin grupo", new[] { "Tenía sin registrar" })]
    [InlineData("otro grupo", new[] { "Tenía sin registrar" })]
    public async Task Sin_cupo_recibe_igual_el_envase_y_lo_anota_como_sin_registrar(string kind, string[] notas)
    {
        // El chofer trae envases que el sistema no le tenía al cliente (por ejemplo, del
        // sistema anterior): se reciben igual, suben al camión y el saldo queda en cero.
        var f = await Seed();
        if (kind is "sin grupo" or "otro grupo")
            await f.Write(async db => (await db.Products.SingleAsync(p => p.Id == f.ProductId)).ExchangeGroup = kind == "sin grupo" ? null : "Soda");
        var devueltos = kind == "exceso" ? 2 : 1;
        var command = Exchange(f, devueltos);
        if (kind == "sin venta") command = command with { Type = DeliveryType.ContainerOnly, Items = [] };
        var result = await f.Send(command);
        Assert.Equal(devueltos, await f.Stock(ContainerState.Empty, f.NoneId));
        Assert.Equal(0, await f.Read(db => db.CustomerContainerBalances.Where(b => b.CustomerId == f.CustomerId && b.ProductId == f.NoneId).Select(b => b.Quantity).SingleOrDefaultAsync()));
        var anotadas = await f.Read(db => db.ContainerMovements.Where(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection)
            .OrderBy(m => m.Notes).Select(m => m.Notes!).ToListAsync());
        Assert.Equal(notas, anotadas);
    }

    [Fact]
    public async Task Saldo_previo_y_entregados_del_mismo_producto_no_generan_cambio()
    {
        var f = await Seed();
        await f.Send(f.Sale(1) with { Items = [new(f.NoneId, 1)] });
        var result = await f.Send(Exchange(f));
        Assert.False(await f.Read(db => db.ContainerMovements.AnyAsync(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection)));
        await f.Send(f.Sale(1, 1));
        Assert.Equal(1, await f.Balance());
    }

    [Fact]
    public async Task Dos_excesos_comparten_el_cupo_del_grupo()
    {
        var f = await Seed();
        var command = Exchange(f) with { ContainersIn = [new(f.NoneId, 1), new(f.UnitId, 1)] };
        // Vendió un solo bidón: cubre un cambio; el otro envase lo tenía sin registrar.
        var result = await f.Send(command);
        Assert.Equal(new[] { "Cambio por Bidon", "Tenía sin registrar" }, await f.Read(db => db.ContainerMovements
            .Where(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection)
            .OrderBy(m => m.Notes).Select(m => m.Notes!).ToListAsync()));
    }

    [Fact]
    public async Task Nota_elige_el_producto_mas_vendido_y_el_mismo_producto_no_da_cupo_extra()
    {
        var f = await Seed();
        var result = await f.Send(Exchange(f) with { Items = [new(f.ProductId, 1), new(f.UnitId, 2)] });
        Assert.Equal("Cambio por Dispenser", await f.Read(db => db.ContainerMovements.Where(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection).Select(m => m.Notes).SingleAsync()));
        // Devolver más del mismo producto ya no se rechaza: lo que sobra lo tenía sin registrar.
        var otra = await f.Send(f.Sale(1, 4));
        Assert.True(await f.Read(db => db.ContainerMovements.AnyAsync(m => m.DeliveryId == otra.DeliveryId && m.Notes == "Tenía sin registrar")));
        Assert.Equal(0, await f.Balance());
    }

    [Fact]
    public async Task Catalogo_recorta_valida_y_permite_quitar_el_grupo()
    {
        var f = await Seed();
        var create = new CreateProductCommand("Bidón " + Guid.NewGuid(), null, 100, ContainerTracking.ByBalance, true, null, ExchangeGroup: " Bidones ");
        var id = await f.Send(create, true);
        Assert.Equal("Bidones", (await f.Send(new GetProductByIdQuery(id), true)).ExchangeGroup);
        foreach (var tracking in new[] { ContainerTracking.None, ContainerTracking.ByUnit })
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(create with { Tracking = tracking }, true));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(create with { ExchangeGroup = new string('a', 51) }, true));
        var update = new UpdateProductCommand(id, create.Detail, null, 100, ContainerTracking.None, true, null, ExchangeGroup: "Bidones");
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(update, true));
        await f.Send(update with { ExchangeGroup = "  " }, true);
        Assert.Null((await f.Send(new GetProductByIdQuery(id), true)).ExchangeGroup);
    }
}
