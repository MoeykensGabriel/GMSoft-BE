using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Removal;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Products.Create;
using GMSoft.Application.Features.Products.Update;
using GMSoft.Application.Features.Products.GetById;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class LendableProductsTests(PostgresFixture fixture)
{
    private async Task<Scenario> Seed()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(async db => { var p = await db.Products.SingleAsync(p => p.Id == f.NoneId); p.CanBeLent = true; });
        return f;
    }

    private static RegisterDeliveryCommand Loan(Scenario f, int quantity = 1) =>
        new(f.CustomerId, null, DeliveryType.ContainerOnly, [], [], [], null, null, Guid.NewGuid(),
            ExpectedSessionId: f.SessionId) { LoansOut = [new(f.NoneId, quantity)] };

    private static Task<int> Balance(Scenario f) => f.Read(db => db.CustomerContainerBalances
        .Where(b => b.CustomerId == f.CustomerId && b.ProductId == f.NoneId).SumAsync(b => b.Quantity));

    [Fact]
    public async Task Vender_no_anota_un_prestamo()
    {
        var f = await Seed();
        var result = await f.Send(f.Sale() with { Items = [new(f.NoneId, 2)] });
        Assert.Equal(100, result.Total);
        Assert.Equal(0, await Balance(f));
        Assert.Equal(18, await f.Stock(product: f.NoneId));
    }

    [Fact]
    public async Task Prestar_sin_venta_mueve_stock_saldo_historial_y_reintenta_sin_duplicar()
    {
        var f = await Seed();
        var command = Loan(f, 2);
        var first = await f.Send(command);
        var retry = await f.Send(command with { ExpectedSessionId = Guid.NewGuid() });
        Assert.Equal(first, retry);
        Assert.Equal(0, first.Total);
        Assert.Equal(2, await Balance(f));
        Assert.Equal(18, await f.Stock(product: f.NoneId));
        var movement = Assert.Single(await f.Read(db => db.ContainerMovements.Where(m => m.DeliveryId == first.DeliveryId).ToListAsync()));
        Assert.Equal(ContainerMovementType.LoanedToCustomer, movement.Type);
        Assert.Equal(f.UserId, movement.RegisteredByUserId);
        using var scope = f.Scope(true);
        var visits = await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetDeliveriesAsync(f.SessionId);
        Assert.Equal(2, Assert.Single(Assert.Single(visits).LoansOut).Quantity);
        var account = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>().GetAccountStatementAsync(f.CustomerId, 100);
        Assert.Contains(account.Rows, r => r.Kind == "Loan" && r.ContainerChanges.Single().Quantity == 2);
    }

    [Fact]
    public async Task Venta_y_prestamo_comparten_el_stock_y_el_rechazo_no_deja_movimientos()
    {
        var f = await Seed();
        var command = f.Sale() with { Items = [new(f.NoneId, 19)], LoansOut = [new(f.NoneId, 2)] };
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command));
        Assert.Equal(0, await f.Sales());
        Assert.Equal(0, await Balance(f));
        var result = await f.Send(command with { LoansOut = [new(f.NoneId, 1)] });
        Assert.Equal(950, result.Total);
        Assert.Equal(0, await f.Stock(product: f.NoneId));
        Assert.Equal(1, await Balance(f));
    }

    [Fact]
    public async Task Retirar_controla_saldo_previo_y_sube_como_vacio()
    {
        var f = await Seed();
        await f.Send(Loan(f));
        var command = Loan(f) with { LoansOut = [], LoansIn = [new(f.NoneId, 2)] };
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command with { LoansOut = [new(f.NoneId, 1)] }));
        var result = await f.Send(command with { LoansIn = [new(f.NoneId, 1)] });
        Assert.Equal(0, result.Total);
        Assert.Equal(0, await Balance(f));
        Assert.Equal(1, await f.Stock(ContainerState.Empty, f.NoneId));
        Assert.Equal(19, await f.Stock(product: f.NoneId));
        Assert.Equal(ContainerMovementType.LoanReturned, await f.Read(db => db.ContainerMovements
            .Where(m => m.DeliveryId == result.DeliveryId).Select(m => m.Type).SingleAsync()));
        using var scope = f.Scope(true);
        var account = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>().GetAccountStatementAsync(f.CustomerId, 100);
        Assert.Contains(account.Rows, r => r.Kind == "LoanReturn" && r.ContainerChanges.Single().Quantity == -1);
        var visits = await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetDeliveriesAsync(f.SessionId);
        Assert.Equal(1, Assert.Single(visits.Single(v => v.DeliveryId == result.DeliveryId).LoansIn).Quantity);
    }

    [Fact]
    public async Task Productos_no_prestables_y_salida_distinta_se_rechazan()
    {
        var f = await Seed();
        foreach (var id in new[] { f.ProductId, f.UnitId })
        {
            await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Loan(f) with { LoansOut = [new(id, 1)] }));
            await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Loan(f) with { LoansOut = [], LoansIn = [new(id, 1)] }));
        }
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Loan(f) with { ExpectedSessionId = Guid.NewGuid() }));
        await f.Write(async db => { (await db.Products.SingleAsync(p => p.Id == f.NoneId)).CanBeLent = false; });
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Loan(f)));
        Assert.Equal(0, await f.Sales());
    }

    [Fact]
    public async Task El_catalogo_conserva_la_casilla_y_rechaza_prestables_con_envase()
    {
        var f = await Seed();
        var create = new CreateProductCommand("Dispenser " + Guid.NewGuid(), null, 5000, ContainerTracking.None, true, null, true);
        var id = await f.Send(create, true);
        Assert.True((await f.Send(new GetProductByIdQuery(id), true)).CanBeLent);
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(create with { Tracking = ContainerTracking.ByBalance }, true));
        var update = new UpdateProductCommand(id, create.Detail, null, 6000, ContainerTracking.ByUnit, true, null, true);
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(update, true));
        await f.Send(update with { Tracking = ContainerTracking.None }, true);
        var product = await f.Send(new GetProductByIdQuery(id), true);
        Assert.True(product.CanBeLent);
        Assert.Equal(6000, product.SalePrice);
    }

    [Fact]
    public async Task Dos_retiros_simultaneos_no_reutilizan_el_mismo_saldo()
    {
        var f = await Seed();
        await f.Send(Loan(f));
        async Task<bool> Return()
        {
            try { await f.Send(Loan(f) with { LoansOut = [], LoansIn = [new(f.NoneId, 1)] }); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Return(), Return());
        Assert.Single(results, success => success);
        Assert.Equal(0, await Balance(f));
        Assert.Equal(1, await f.Stock(ContainerState.Empty, f.NoneId));
    }

    [Fact]
    public async Task La_baja_cuenta_el_dispenser_prestado_como_envase_por_retirar()
    {
        var f = await Seed();
        await f.Send(Loan(f));
        var request = new RequestRemoval(f.CustomerId, Guid.NewGuid(), RemovalReason.Moved, null, false);
        var error = await Assert.ThrowsAsync<ContainersOutstandingException>(() => f.Send(request));
        Assert.Equal(f.NoneId, Assert.Single(error.Containers).ProductId);
        var removal = await f.Send(request with { ContainersNotReturned = true });
        Assert.Equal(1, Assert.Single(removal.Containers).Quantity);
    }
}
