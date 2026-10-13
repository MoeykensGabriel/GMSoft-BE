using GMSoft.Application.Common.Exceptions;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class ConcurrencyTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "C")]
    public async Task Recargas_simultaneas_con_misma_clave_guardan_una_sola_tanda()
    {
        var f = await new Scenario(fixture).Seed(); var command = f.Restock();
        // Both initial idempotency lookups miss, forcing the real unique-index race.
        var barrier = new SqlReadBarrier("SessionRestocks", "ClientRequestId");
        var results = await Task.WhenAll(f.Send(command, true, barrier), f.Send(command, true, barrier));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(1, await f.Read(db => db.SessionRestocks.CountAsync(r => r.DeliverySessionId == f.SessionId)));
        Assert.Equal(23, await f.Stock()); Assert.Equal(22, await f.Stock(product: f.NoneId));
        Assert.Equal(2, await f.Read(db => db.SessionStockMovements.CountAsync(m => m.DeliverySessionId == f.SessionId && m.Type == SessionStockMovementType.Restock)));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Recarga_y_cierre_simultaneos_no_agregan_stock_despues_de_la_recepcion()
    {
        // Either the restock commits before reception, becoming part of its shortage,
        // or reception wins and the restock returns Conflict. Existing restocks remain
        // valid history on closed sessions; they must not disappear on closure.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var f = await new Scenario(fixture).Seed();
            var restock = Record.ExceptionAsync(() => f.Send(f.Restock(), true));
            var close = f.Send(f.Close(), true);
            await Task.WhenAll(restock, close);
            Assert.True(await restock is null or ConflictException);
            var result = await close;
            var persisted = await f.Read(db => db.SessionRestocks.CountAsync(r => r.DeliverySessionId == f.SessionId));
            Assert.Equal(await restock is null ? 1 : 0, persisted);
            Assert.Equal(persisted * 3, await f.Stock());
            Assert.Equal(persisted * 3, result.Faltante.SingleOrDefault(p => p.ProductId == f.ProductId)?.FullOnBoard ?? 0);
            Assert.Equal(SessionStatus.Closed, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.Status).SingleAsync()));
            await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Restock(), true));
            Assert.Equal(persisted * 3, await f.Stock());
        }
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Reintentos_simultaneos_de_venta_devuelven_ambos_la_venta_original()
    {
        // Both first idempotency lookups miss before either request takes the session lock.
        var f = await new Scenario(fixture).Seed(); var barrier = new SqlReadBarrier("Deliveries", "ClientRequestId");
        var sale = f.Sale() with { Items = [new(f.NoneId, 3)] };
        var results = await Task.WhenAll(f.Send(sale, barrier: barrier), f.Send(sale, barrier: barrier));
        Assert.Equal(results[0].DeliveryId, results[1].DeliveryId);
        Assert.Equal(1, await f.Sales()); Assert.Equal(17, await f.Stock(product: f.NoneId));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Ventas_simultaneas_no_superan_el_stock_disponible()
    {
        var f = await new Scenario(fixture).Seed(); var other = f.Customer(8);
        await f.Write(db => { db.Customers.Add(other); return Task.CompletedTask; });
        // Both requests have read their open session before either takes its row lock.
        var barrier = new SqlReadBarrier("DeliverySessions", "\"DriverId\" =");
        var sale = f.Sale(15) with { Items = [new(f.NoneId, 15)] };
        var errors = await Task.WhenAll(Record.ExceptionAsync(() => f.Send(sale, barrier: barrier)),
            Record.ExceptionAsync(() => f.Send(sale with { CustomerId = other.Id, ClientRequestId = Guid.NewGuid() }, barrier: barrier)));
        Assert.Single(errors, e => e is null);
        Assert.IsType<ConflictException>(Assert.Single(errors, e => e is not null));
        Assert.Equal(1, await f.Sales()); Assert.Equal(5, await f.Stock(product: f.NoneId));
    }
}
