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
    [InlineData("exceso")]
    [InlineData("sin venta")]
    [InlineData("sin grupo")]
    [InlineData("otro grupo")]
    public async Task Rechaza_sin_cupo_y_no_deja_movimientos(string kind)
    {
        var f = await Seed();
        if (kind is "sin grupo" or "otro grupo")
            await f.Write(async db => (await db.Products.SingleAsync(p => p.Id == f.ProductId)).ExchangeGroup = kind == "sin grupo" ? null : "Soda");
        var command = Exchange(f, kind == "exceso" ? 2 : 1);
        if (kind == "sin venta") command = command with { Type = DeliveryType.ContainerOnly, Items = [] };
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command));
        Assert.Equal(0, await f.Sales());
        Assert.Equal(0, await f.Stock(ContainerState.Empty, f.NoneId));
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
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command));
        var result = await f.Send(command with { Items = [new(f.ProductId, 2)] });
        Assert.Equal(2, await f.Read(db => db.ContainerMovements.CountAsync(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection)));
    }

    [Fact]
    public async Task Nota_elige_el_producto_mas_vendido_y_el_mismo_producto_no_da_cupo_extra()
    {
        var f = await Seed();
        var result = await f.Send(Exchange(f) with { Items = [new(f.ProductId, 1), new(f.UnitId, 2)] });
        Assert.Equal("Cambio por Dispenser", await f.Read(db => db.ContainerMovements.Where(m => m.DeliveryId == result.DeliveryId && m.Type == ContainerMovementType.ExchangeCorrection).Select(m => m.Notes).SingleAsync()));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Sale(1, 4)));
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
