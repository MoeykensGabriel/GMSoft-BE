using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Application.Features.VehicleLoads.UpdateDays;
using GMSoft.Application.Features.VehicleLoads.Clear;
using GMSoft.Application.Features.VehicleLoads.Remove;
using GMSoft.Domain.Entities;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Promotions.Close;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class ConcurrencyTests(PostgresFixture fixture)
{
    [Theory, InlineData(false), InlineData(true), Trait("Grupo", "C")]
    public async Task Aperturas_simultaneas_consumen_la_carga_una_sola_vez(bool differentDriver)
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Load();
        var otherDriverId = f.DriverId;
        if (differentDriver)
        {
            otherDriverId = Guid.NewGuid();
            await f.Write(db =>
            {
                db.Drivers.Add(new Driver { Id = otherDriverId, FirstName = "Otro", LastName = "Chofer",
                    DocumentNumber = otherDriverId.ToString(), VehicleId = f.VehicleId });
                return Task.CompletedTask;
            });
        }
        var barrier = new SqlReadBarrier(differentDriver ? "Vehicles" : "Drivers", "FOR UPDATE", true);
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => f.Send(new OpenSessionCommand(f.ZoneId, 100, DriverId: f.DriverId), true, barrier)),
            Record.ExceptionAsync(() => f.Send(new OpenSessionCommand(f.ZoneId, 100, DriverId: otherDriverId), true, barrier)));
        Assert.Single(errors, e => e is null);
        Assert.IsType<ConflictException>(Assert.Single(errors, e => e is not null));
        var session = Assert.Single(await f.Read(db => db.DeliverySessions.Where(s => s.VehicleId == f.VehicleId).ToListAsync()));
        f.SessionId = session.Id;
        Assert.Equal(20, await f.Stock());
        Assert.Equal(3, await f.Read(db => db.VehicleLoads.CountAsync(l => l.ConsumedBySessionId == session.Id)));
        Assert.Equal(0, await f.Read(db => db.VehicleLoads.CountAsync(l => l.VehicleId == f.VehicleId && l.ConsumedBySessionId == null)));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Cargas_simultaneas_con_misma_clave_devuelven_la_respuesta_original()
    {
        var f = await new Scenario(fixture).Seed(false);
        var command = new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 5), new(f.NoneId, 3)], [1], Guid.NewGuid());
        var barrier = new SqlReadBarrier("VehicleLoads", "ClientRequestId");
        var results = await Task.WhenAll(f.Send(command, true, barrier), f.Send(command, true, barrier));
        Assert.Equal(results[0], results[1]);
        var loads = await f.Read(db => db.VehicleLoads.Where(l => l.VehicleId == f.VehicleId).ToListAsync());
        Assert.Equal(2, loads.Count);
        Assert.Equal(8, loads.Sum(l => l.Quantity));
        Assert.All(loads, l => Assert.Equal(results[0].LoadedAt, l.LoadedAt));
    }

    [Theory, InlineData("days"), InlineData("clear"), InlineData("remove"), InlineData("load"), Trait("Grupo", "C")]
    public async Task Apertura_y_cambios_de_carga_no_reviven_cargas_consumidas(string operation)
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Load();
        var loadId = await f.Read(db => db.VehicleLoads.Where(l => l.VehicleId == f.VehicleId && l.ProductId == f.ProductId)
            .Select(l => l.Id).SingleAsync());
        var barrier = new SqlReadBarrier("Vehicles", "FOR UPDATE", true);
        async Task Change()
        {
            switch (operation)
            {
                case "days": await f.Send(new UpdateVehicleRouteDaysCommand(f.VehicleId, [2, 3]), true, barrier); break;
                case "clear": await f.Send(new ClearVehicleLoadCommand(f.VehicleId), true, barrier); break;
                case "remove": await f.Send(new RemoveVehicleLoadCommand(f.VehicleId, loadId), true, barrier); break;
                case "load": await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 5)], [2]), true, barrier); break;
            }
        }
        var opening = Record.ExceptionAsync(async () => f.SessionId = await f.Send(new OpenSessionCommand(f.ZoneId, 100), barrier: barrier));
        var change = Record.ExceptionAsync(Change);
        await Task.WhenAll(opening, change);
        Assert.True(await change is null or ConflictException);
        Assert.True(await opening is null or ConflictException);
        Assert.True(await opening is null || await change is null);
        var loads = await f.Read(db => db.VehicleLoads.IgnoreQueryFilters().Where(l => l.VehicleId == f.VehicleId).ToListAsync());
        if (await opening is null)
        {
            Assert.All(loads.Where(l => !l.IsDeleted), l => Assert.Equal(f.SessionId, l.ConsumedBySessionId));
            Assert.All(loads.Where(l => l.IsDeleted), l => Assert.Null(l.ConsumedBySessionId));
            Assert.Equal(loads.Where(l => !l.IsDeleted && l.ProductId == f.ProductId).Sum(l => l.Quantity), await f.Stock());
            var days = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.RouteDays).SingleAsync());
            Assert.All(loads.Where(l => !l.IsDeleted), l => Assert.Equal(days, l.RouteDays));
            if (operation is "days" or "load")
                Assert.Equal(await change is null ? (operation == "days" ? new[] { 2, 3 } : [2]) : [1], days);
        }
        else
        {
            Assert.Equal("clear", operation);
            Assert.All(loads, l => { Assert.True(l.IsDeleted); Assert.Null(l.ConsumedBySessionId); });
            Assert.Equal(0, await f.Read(db => db.DeliverySessions.CountAsync(s => s.VehicleId == f.VehicleId)));
        }
    }

    [Theory, InlineData(false), InlineData(true), Trait("Grupo", "C")]
    public async Task Indices_de_salida_abierta_traducen_duplicados_a_conflicto(bool sameDriver)
    {
        var f = await new Scenario(fixture).Seed();
        var other = await new Scenario(fixture).Seed(false);
        await Assert.ThrowsAsync<ConflictException>(() => f.Write(db =>
        {
            db.DeliverySessions.Add(new DeliverySession { DriverId = sameDriver ? f.DriverId : other.DriverId,
                VehicleId = sameDriver ? other.VehicleId : f.VehicleId, ZoneId = f.ZoneId,
                OpenedAt = DateTime.UtcNow, Status = SessionStatus.Open });
            return Task.CompletedTask;
        }));
        Assert.Equal(1, await f.Read(db => db.DeliverySessions.CountAsync(s => s.DriverId == f.DriverId)));
        Assert.Equal(1, await f.Read(db => db.DeliverySessions.CountAsync(s => s.VehicleId == f.VehicleId)));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Recepciones_simultaneas_descargan_una_sola_vez()
    {
        var f = await new Scenario(fixture).Seed();
        var barrier = new SqlReadBarrier("DeliverySessions", "LIMIT 1");
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => f.Send(f.Close(), true, barrier)),
            Record.ExceptionAsync(() => f.Send(f.Close(), true, barrier)));
        Assert.Single(errors, e => e is null);
        Assert.IsType<ConflictException>(Assert.Single(errors, e => e is not null));
        Assert.Equal(0, await f.Stock());
        Assert.Equal(3, await f.Read(db => db.SessionStockMovements.CountAsync(m =>
            m.DeliverySessionId == f.SessionId && m.Type == SessionStockMovementType.ReturnedAtClose)));
        var session = await f.Read(db => db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId));
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(110, session.KilometersAtClose);
        Assert.NotNull(session.ClosedAt);
    }

    [Theory, InlineData(false), InlineData(true), Trait("Grupo", "C")]
    public async Task Cierre_promocion_y_recepcion_no_reabren_la_salida(bool convert)
    {
        var f = await new Scenario(fixture).Seed();
        var promotion = await f.Send(new RegisterPromotionCommand(
            new(null, "Prospecto", "123", "Calle", null, [1]), [new(f.ProductId, 2)], Guid.NewGuid()));
        var barrier = new SqlReadBarrier("DeliverySessions", "LIMIT 1");
        var closePromotion = Record.ExceptionAsync(() => f.Send(new ClosePromotionCommand(
            promotion.PromotionId, Guid.NewGuid(), convert, null, convert ? [] : [new(f.ProductId, 2)]), barrier: barrier));
        var reception = f.Send(f.Close(18), true, barrier);
        await Task.WhenAll(closePromotion, reception);
        Assert.True(await closePromotion is null or ConflictException);
        var session = await f.Read(db => db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId));
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(110, session.KilometersAtClose);
        Assert.Equal(f.AdminId, session.ClosedByUserId);
        Assert.NotNull(session.ClosedAt);
        var stored = await f.Read(db => db.Promotions.SingleAsync(p => p.Id == promotion.PromotionId));
        Assert.Equal(await closePromotion is null
            ? (convert ? PromotionStatus.Converted : PromotionStatus.NotConverted) : PromotionStatus.Pending, stored.Status);
        Assert.Equal(await closePromotion is null && !convert ? 2 : 0, await f.Stock(ContainerState.Empty));
        Assert.Equal(await closePromotion is null && convert ? 2 : 1,
            await f.Read(db => db.Customers.CountAsync(c => c.VehicleId == f.VehicleId)));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Rendiciones_simultaneas_guardan_una_sola_rendicion()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true);
        var command = new RegisterSettlementCommand(f.SessionId, 123, "Rendicion");
        var barrier = new SqlReadBarrier("DeliverySessions", "FOR UPDATE", true);
        var errors = await Task.WhenAll(Record.ExceptionAsync(() => f.Send(command, true, barrier)),
            Record.ExceptionAsync(() => f.Send(command, true, barrier)));
        Assert.Single(errors, e => e is null);
        Assert.IsType<ConflictException>(Assert.Single(errors, e => e is not null));
        var settlement = Assert.Single(await f.Read(db => db.SessionCashSettlements
            .Where(s => s.DeliverySessionId == f.SessionId).ToListAsync()));
        Assert.Equal(123, settlement.AmountReceived);
        Assert.Equal(f.AdminId, settlement.ReceivedByUserId);
    }

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
