using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Close;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Reports.ContainersOut;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Data.Context;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class WorkflowTests(PostgresFixture fixture)
{
    private Task<Scenario> New(bool open = true) => new Scenario(fixture).Seed(open);

    [Fact, Trait("Grupo", "A")]
    public async Task Todas_las_migraciones_aplican_desde_cero()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(fixture.DatabaseName, db.Database.GetDbConnection().Database);
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(applied);
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
    }

    [Fact, Trait("Grupo", "A")]
    public async Task Modelo_coincide_con_snapshot_migrado()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Venta_descuenta_llenos_y_genera_saldo_ByBalance()
    {
        var f = await New(); await f.Send(f.Sale(4, 1));
        Assert.Equal(16, await f.Stock()); Assert.Equal(1, await f.Stock(ContainerState.Empty));
        Assert.Equal(3, await f.Balance());
        Assert.Equal(400, await f.Read(db => db.Deliveries.Where(d => d.DeliverySessionId == f.SessionId).SumAsync(d => d.Total)));
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Cobro_completo_en_efectivo_usa_total_del_servidor()
    {
        var f = await New(); var result = await f.Send(f.Sale(paid: true));
        var payment = await f.Read(db => db.Payments.SingleAsync(p => p.DeliverySessionId == f.SessionId));
        Assert.Equal(300, payment.Amount); Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(0, result.SaldoCuentaCliente); Assert.Equal(1, await f.Sales());
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Venta_sin_cobro_deja_deuda_y_no_crea_pago()
    {
        var f = await New(); var result = await f.Send(f.Sale());
        Assert.Equal(300, result.SaldoCuentaCliente);
        Assert.False(await f.Read(db => db.Payments.AnyAsync(p => p.DeliverySessionId == f.SessionId)));
        Assert.Equal(300, await f.Read(db => db.Deliveries.Where(d => d.DeliverySessionId == f.SessionId).SumAsync(d => d.Total)));
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Stock_insuficiente_no_escribe_venta_cliente_pago_ni_envases()
    {
        var f = await New();
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Sale(21, paid: true) with
            { CustomerId = null, NewCustomer = new(null, "Nuevo", "123", "Calle", null, [1]) }));
        Assert.Equal(0, await f.Sales()); Assert.Equal(20, await f.Stock()); Assert.Equal(0, await f.Balance());
        Assert.Equal(1, await f.Read(db => db.Customers.CountAsync(c => c.ZoneId == f.ZoneId)));
        Assert.False(await f.Read(db => db.Payments.AnyAsync(p => p.DeliverySessionId == f.SessionId)));
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Alta_en_la_calle_queda_al_final_del_recorrido()
    {
        var f = await New(); var result = await f.Send(f.Sale() with
            { CustomerId = null, NewCustomer = new(null, "Nuevo", "123", "Calle", null, [5, 1]) });
        var customer = await f.Read(db => db.Customers.SingleAsync(c => c.Id == result.CustomerId));
        Assert.Equal(8, customer.RouteOrder); Assert.Equal(f.ZoneId, customer.ZoneId);
        Assert.Equal(f.VehicleId, customer.VehicleId); Assert.Equal(new[] { 1, 5 }, customer.VisitDays);
        Assert.Equal(1, await f.Sales());
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Reintento_de_venta_no_duplica_venta_stock_saldo_ni_pago()
    {
        var f = await New(); var command = f.Sale(paid: true);
        var first = await f.Send(command); var retry = await f.Send(command);
        Assert.Equal(first, retry); Assert.Equal(1, await f.Sales()); Assert.Equal(17, await f.Stock());
        Assert.Equal(3, await f.Balance());
        Assert.Equal(1, await f.Read(db => db.Payments.CountAsync(p => p.DeliverySessionId == f.SessionId)));
    }

    [Fact, Trait("Grupo", "B")]
    public async Task Devolucion_mayor_al_saldo_se_recibe_y_el_saldo_queda_en_cero()
    {
        // Cliente nuevo en la puerta que además devuelve un envase que tenía de antes: se
        // recibe igual (el chofer lo trae porque lo tenía) y su saldo no queda negativo.
        var f = await New();
        var result = await f.Send(f.Sale(3, 4) with
            { CustomerId = null, NewCustomer = new(null, "Nuevo", "123", "Calle", null, [1]) });
        Assert.Equal(1, await f.Sales()); Assert.Equal(17, await f.Stock());
        Assert.Equal(4, await f.Stock(ContainerState.Empty));
        Assert.Equal(0, await f.Read(db => db.CustomerContainerBalances.Where(b => b.CustomerId == result.CustomerId && b.ProductId == f.ProductId).Select(b => b.Quantity).SingleAsync()));
        Assert.True(await f.Read(db => db.ContainerMovements.AnyAsync(m => m.DeliveryId == result.DeliveryId && m.Notes == "Tenía sin registrar")));
    }

    [Fact, Trait("Grupo", "D")]
    public async Task Sin_carga_pendiente_no_se_abre_salida()
    {
        var f = await New(false);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new OpenSessionCommand(f.ZoneId, 100)));
        Assert.False(await f.Read(db => db.DeliverySessions.AnyAsync(s => s.VehicleId == f.VehicleId)));
    }

    [Fact, Trait("Grupo", "D")]
    public async Task Apertura_consume_carga_y_copia_dias_y_stock()
    {
        var f = await New();
        var loads = await f.Read(db => db.VehicleLoads.Where(l => l.VehicleId == f.VehicleId).ToListAsync());
        Assert.Equal(3, loads.Count); Assert.All(loads, l => Assert.Equal(f.SessionId, l.ConsumedBySessionId));
        Assert.Equal(20, await f.Stock());
        Assert.Equal(new[] { 1 }, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.RouteDays).SingleAsync()));
    }

    [Fact, Trait("Grupo", "D")]
    public async Task Camion_con_salida_abierta_rechaza_carga_de_deposito()
    {
        var f = await New(); await Assert.ThrowsAsync<ConflictException>(() => f.Load());
        Assert.Equal(3, await f.Read(db => db.VehicleLoads.CountAsync(l => l.VehicleId == f.VehicleId)));
        Assert.Equal(20, await f.Stock());
    }

    [Fact, Trait("Grupo", "D")]
    public async Task Kilometraje_menor_al_ultimo_no_consume_carga()
    {
        var f = await New(false); await f.Load();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(new OpenSessionCommand(f.ZoneId, 99)));
        Assert.Equal(3, await f.Read(db => db.VehicleLoads.CountAsync(l => l.VehicleId == f.VehicleId && l.ConsumedBySessionId == null)));
        Assert.Equal(100, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
    }

    [Fact, Trait("Grupo", "E")]
    public async Task Recepcion_completa_cuadra_y_cierra_salida()
    {
        var f = await New(); await f.Send(f.Sale(3, 2));
        var result = await f.Send(f.Close(17, 2), true);
        Assert.True(result.CuadraTodo); Assert.Empty(result.Faltante);
        Assert.Equal(0, await f.Stock()); Assert.Equal(0, await f.Stock(ContainerState.Empty));
        Assert.Equal(SessionStatus.Closed, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.Status).SingleAsync()));
    }

    [Fact, Trait("Grupo", "E")]
    public async Task Recepcion_incompleta_informa_faltantes_de_llenos_y_vacios()
    {
        var f = await New(); await f.Send(f.Sale(3, 2)); var result = await f.Send(f.Close(15, 1), true);
        Assert.False(result.CuadraTodo); var missing = Assert.Single(result.Faltante);
        Assert.Equal(2, missing.FullOnBoard); Assert.Equal(1, missing.EmptyOnBoard);
        Assert.Equal(missing.FullOnBoard, await f.Stock()); Assert.Equal(missing.EmptyOnBoard, await f.Stock(ContainerState.Empty));
    }

    [Fact, Trait("Grupo", "E")]
    public async Task Rendicion_con_diferencia_y_detalle_por_cliente_coinciden_con_libro_mayor()
    {
        var f = await New(); await f.Send(f.Sale(3, 2, true)); await f.Send(f.Sale(1));
        await f.Send(f.Close(16, 2), true);
        var settlement = await f.Send(new RegisterSettlementCommand(f.SessionId, 250, "Caja"), true);
        Assert.Equal(50, settlement.CashDifference); Assert.Equal(400, settlement.TotalSold);
        var detail = Assert.Single(await f.Send(new GetSessionDetailedSettlementQuery(f.SessionId), true));
        Assert.Equal(400, detail.Lines.Sum(l => l.Amount)); Assert.Equal(4, detail.Lines.Sum(l => l.Quantity));
        Assert.Equal(2, Assert.Single(detail.ReturnedContainers).Quantity); Assert.Equal(300, detail.Cash); Assert.Equal(100, detail.Balance);
        Assert.Equal(250, await f.Read(db => db.SessionCashSettlements.Where(s => s.DeliverySessionId == f.SessionId).Select(s => s.AmountReceived).SingleAsync()));
        Assert.Equal(2, await f.Balance());
    }

    [Fact, Trait("Grupo", "F")]
    public async Task Recarga_guarda_varios_productos_como_una_tanda()
    {
        var f = await New(); var result = await f.Send(f.Restock(), true);
        var movements = await f.Read(db => db.SessionStockMovements.Where(m => m.SessionRestockId == result.Id).ToListAsync());
        Assert.Equal(2, movements.Count); Assert.Equal(5, movements.Sum(m => m.Quantity));
        Assert.All(movements, m => { Assert.Equal(result.OccurredAt, m.OccurredAt); Assert.Equal(f.AdminId, m.RegisteredByUserId); });
        Assert.Equal(1, await f.Read(db => db.SessionRestocks.CountAsync(r => r.DeliverySessionId == f.SessionId)));
    }

    [Fact, Trait("Grupo", "F")]
    public async Task Salida_actual_del_chofer_refleja_stock_y_lista_de_recargas()
    {
        var f = await New(); var restock = await f.Send(f.Restock(), true);
        var current = await f.Send(new GetCurrentSessionQuery()); Assert.NotNull(current);
        Assert.Equal(restock.Id, Assert.Single(current.Restocks).Id);
        Assert.Equal(await f.Stock(), current.Stock.Single(s => s.ProductId == f.ProductId).FullOnBoard);
        Assert.Equal(23, await f.Stock()); Assert.Equal(22, await f.Stock(product: f.NoneId));
    }

    [Fact, Trait("Grupo", "F")]
    public async Task Recarga_de_salida_cerrada_devuelve_conflicto_sin_movimientos()
    {
        var f = await New(); await f.Send(f.Close(), true);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Restock(), true));
        Assert.False(await f.Read(db => db.SessionRestocks.AnyAsync(r => r.DeliverySessionId == f.SessionId)));
        Assert.Equal(0, await f.Stock());
    }

    private static RegisterPromotionCommand Promotion(Scenario f) => new(new(null, "Prospecto", "123", "Calle", null, [1]),
        [new(f.ProductId, 3)], Guid.NewGuid());

    [Fact, Trait("Grupo", "G")]
    public async Task Promocion_descuenta_llenos_y_presta_envases_sin_crear_cliente()
    {
        var f = await New(); var result = await f.Send(Promotion(f));
        Assert.Null(result.CustomerId); Assert.Equal(17, await f.Stock()); Assert.Equal(0, await f.Sales());
        Assert.Equal(1, await f.Read(db => db.Customers.CountAsync(c => c.ZoneId == f.ZoneId)));
        Assert.Equal(3, await f.Read(db => db.PromotionLines.Where(l => l.PromotionId == result.PromotionId).SumAsync(l => l.ContainersLoaned)));
        Assert.False(await f.Read(db => db.Payments.AnyAsync(p => p.DeliverySessionId == f.SessionId)));
    }

    [Fact, Trait("Grupo", "G")]
    public async Task Conversion_crea_cliente_y_traspasa_envases_sin_venta()
    {
        var f = await New(); var promotion = await f.Send(Promotion(f));
        var result = await f.Send(new ClosePromotionCommand(promotion.PromotionId, Guid.NewGuid(), true, null, []));
        var customer = await f.Read(db => db.Customers.SingleAsync(c => c.Id == result.CustomerId));
        Assert.Equal(8, customer.RouteOrder); Assert.Null(customer.LastVisitAt);
        Assert.Equal(3, await f.Read(db => db.CustomerContainerBalances.Where(b => b.CustomerId == customer.Id).SumAsync(b => b.Quantity)));
        Assert.Equal(PromotionStatus.Converted, await f.Read(db => db.Promotions.Where(p => p.Id == promotion.PromotionId).Select(p => p.Status).SingleAsync()));
        Assert.Equal(17, await f.Stock()); Assert.Equal(0, await f.Sales());
    }

    [Fact, Trait("Grupo", "G")]
    public async Task Cierre_sin_conversion_registra_perdida_y_recupera_vacios()
    {
        var f = await New(); var promotion = await f.Send(Promotion(f));
        await f.Send(new ClosePromotionCommand(promotion.PromotionId, Guid.NewGuid(), false, null, [new(f.ProductId, 2)]));
        var line = await f.Read(db => db.PromotionLines.SingleAsync(l => l.PromotionId == promotion.PromotionId));
        Assert.Equal(1, line.ContainersLost); Assert.Equal(2, line.ContainersReturned); Assert.Equal(2, await f.Stock(ContainerState.Empty));
        Assert.Equal(-1, await f.Read(db => db.PromotionContainerMovements.Where(m => m.PromotionId == promotion.PromotionId && m.ContainerMovement.Type == ContainerMovementType.Lost).SumAsync(m => m.ContainerMovement.Quantity)));
    }

    [Fact, Trait("Grupo", "G")]
    public async Task Segundo_cierre_de_promocion_se_rechaza_sin_duplicar_perdida()
    {
        var f = await New(); var p = await f.Send(Promotion(f));
        await f.Send(new ClosePromotionCommand(p.PromotionId, Guid.NewGuid(), false, null, []));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new ClosePromotionCommand(p.PromotionId, Guid.NewGuid(), true, null, [])));
        Assert.Equal(1, await f.Read(db => db.Customers.CountAsync(c => c.ZoneId == f.ZoneId)));
        Assert.Equal(-3, await f.Read(db => db.PromotionContainerMovements.Where(m => m.PromotionId == p.PromotionId && m.ContainerMovement.Type == ContainerMovementType.Lost).SumAsync(m => m.ContainerMovement.Quantity)));
    }

    [Fact, Trait("Grupo", "G")]
    public async Task Reporte_de_envases_afuera_incluye_prospectos_pendientes()
    {
        var f = await New(); var p = await f.Send(Promotion(f));
        var report = (await f.Send(new GetContainersOutReportQuery(), true)).Single(r => r.ProductId == f.ProductId);
        Assert.Equal(3, report.QuantityOut); Assert.Equal(3, report.PromotionQuantityOut); Assert.Equal(0, report.CustomersHolding);
        Assert.Equal(p.PromotionId, Assert.Single(report.Promotions!).PromotionId);
        Assert.Equal(report.PromotionQuantityOut, await f.Read(db => db.PromotionLines.Where(l => l.PromotionId == p.PromotionId).SumAsync(l => l.ContainersLoaned)));
    }

    [Fact, Trait("Grupo", "H")]
    public async Task Listado_de_clientes_sale_en_orden_de_recorrido()
    {
        var f = await New(); var early = f.Customer(2); var late = f.Customer(11);
        await f.Write(db => { db.Customers.AddRange(late, early); return Task.CompletedTask; });
        var result = await f.Send(new GetCustomersQuery(ZoneId: f.ZoneId), true);
        Assert.Equal(new[] { early.Id, f.CustomerId, late.Id }, result.Items.Select(c => c.Id));
        Assert.Equal(new[] { 2, 7, 11 }, await f.Read(db => db.Customers.Where(c => c.ZoneId == f.ZoneId).OrderBy(c => c.RouteOrder).Select(c => c.RouteOrder).ToArrayAsync()));
    }

    [Fact, Trait("Grupo", "H")]
    public async Task Guardar_recorrido_reasigna_solo_posiciones_de_su_lista()
    {
        var f = await New(); var other = f.Customer(2); var excluded = f.Customer(5, [2]);
        await f.Write(db => { db.Customers.AddRange(other, excluded); return Task.CompletedTask; });
        var route = await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true);
        await f.Send(new SaveRoutePlanningCommand(f.VehicleId, f.ZoneId, 1, route.Version, [f.CustomerId, other.Id], []), true);
        var rows = await f.Read(db => db.Customers.Where(c => c.ZoneId == f.ZoneId).ToDictionaryAsync(c => c.Id, c => c.RouteOrder));
        Assert.Equal(2, rows[f.CustomerId]); Assert.Equal(7, rows[other.Id]); Assert.Equal(5, rows[excluded.Id]);
    }

    [Fact, Trait("Grupo", "H")]
    public async Task Version_vieja_del_recorrido_da_conflicto_sin_cambios()
    {
        var f = await New(); var other = f.Customer(2);
        await f.Write(db => { db.Customers.Add(other); return Task.CompletedTask; });
        var route = await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true);
        await f.Send(new SaveRoutePlanningCommand(f.VehicleId, f.ZoneId, 1, route.Version, [f.CustomerId, other.Id], []), true);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new SaveRoutePlanningCommand(f.VehicleId, f.ZoneId, 1,
            route.Version, [other.Id, f.CustomerId], [new(f.CustomerId, [3])]), true));
        var customer = await f.Read(db => db.Customers.SingleAsync(c => c.Id == f.CustomerId));
        Assert.Equal(2, customer.RouteOrder); Assert.Equal(new[] { 1 }, customer.VisitDays);
    }

    [Theory, Trait("Grupo", "H")]
    [InlineData(0, "White")]
    [InlineData(2, "Red")]
    [InlineData(4, "Black")]
    public async Task Actividad_cuenta_semanas_con_salidas_recibidas_reales(int weeks, string status)
    {
        var f = await New(false);
        // Historical dates are persisted test data, not a fake clock. Every departure is
        // opened and received through the production handlers before dating its history.
        var baseline = DateTime.UtcNow.AddDays(-70);
        await f.Write(async db => { await db.Customers.Where(c => c.Id == f.CustomerId).ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, baseline)); });
        for (var i = 0; i < weeks; i++)
        {
            await f.Load(); f.SessionId = await f.Send(new OpenSessionCommand(f.ZoneId, 100 + i * 10));
            await f.Send(f.Close() with { KilometersAtClose = 110 + i * 10 }, true);
            var opened = DateTime.UtcNow.AddDays(-42 + i * 7);
            await f.Write(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.OpenedAt, opened).SetProperty(x => x.ClosedAt, opened.AddHours(1))));
        }
        // An open departure must not add a missed week.
        await f.Load(); f.SessionId = await f.Send(new OpenSessionCommand(f.ZoneId, 100 + weeks * 10));
        var result = await f.Send(new GetCustomerByIdQuery(f.CustomerId), true);
        Assert.Equal(weeks, result.WeeksWithoutPurchase); Assert.Equal(status, result.ActivityStatus);
        Assert.Equal(weeks, await f.Read(db => db.DeliverySessions.CountAsync(s => s.VehicleId == f.VehicleId && s.Status == SessionStatus.Closed)));
    }

    private static async Task<DailySummaryDto> Summary(Scenario f)
    {
        var opened = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        return await f.Send(new GetDailySummaryQuery(f.VehicleId, DateOnly.FromDateTime(opened.Add(BusinessTime.Offset))), true);
    }

    [Fact, Trait("Grupo", "I")]
    public async Task Resumen_cerrado_coincide_con_faltante_de_recepcion()
    {
        var f = await New(); await f.Send(f.Sale(3, 2)); var close = await f.Send(f.Close(15, 1), true);
        var summary = await Summary(f); var missing = Assert.Single(close.Faltante);
        Assert.True(summary.DayTotals.IsClosed); Assert.Equal(missing.FullOnBoard, summary.DayTotals.Totals.FullDifference);
        Assert.Equal(missing.EmptyOnBoard, summary.DayTotals.Totals.EmptyDifference);
        Assert.Equal(await f.Stock(), summary.DayTotals.Totals.FullDifference);
    }

    [Fact, Trait("Grupo", "I")]
    public async Task Resumen_abierto_muestra_stock_a_bordo_como_diferencia()
    {
        var f = await New(); await f.Send(f.Sale(3, 2)); var summary = await Summary(f);
        Assert.False(summary.DayTotals.IsClosed);
        var product = summary.DayTotals.Products.Single(p => p.ProductId == f.ProductId);
        Assert.Equal(await f.Stock(), product.Stock.FullDifference); Assert.Equal(await f.Stock(ContainerState.Empty), product.Stock.EmptyDifference);
        Assert.Equal(0, product.Stock.FullReturned);
    }

    [Fact, Trait("Grupo", "I")]
    public async Task Efectivo_a_rendir_suma_cobros_y_separa_transferencias()
    {
        var f = await New(); await f.Send(f.Sale(paid: true));
        await f.Send(f.Sale(2) with { Payment = new(null, PaymentMethod.Transfer) });
        var summary = await Summary(f);
        Assert.Equal(300, summary.DayTotals.Money.CashExpected); Assert.Equal(200, summary.DayTotals.Money.Transfer);
        Assert.Equal(await f.Read(db => db.Payments.Where(p => p.DeliverySessionId == f.SessionId && p.Method == PaymentMethod.Cash).SumAsync(p => p.Amount)), summary.DayTotals.Money.CashExpected);
        Assert.Null(summary.DayTotals.Money.CashDeclared);
    }

}
