using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.Correction;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// La oficina corrige una recepcion ya cerrada: lo descargado, el kilometraje y la
/// plata. Nada se borra; queda el antes y el despues con el motivo.
/// </summary>
[Collection("PostgreSQL")]
public class ReceptionCorrectionTests(PostgresFixture fixture)
{
    private static CorrectReceptionCommand Correct(Scenario f, int? km = null, decimal? amount = null,
        params CorrectReturnLine[] returns) => new(f.SessionId, " Se contó mal ", km, amount, returns);

    [Fact]
    public async Task Corregir_lo_descargado_recalcula_el_faltante_y_deja_el_historial()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(full: 18), true);
        Assert.Equal(2, await f.Stock());

        var dto = await f.Send(Correct(f, returns: new CorrectReturnLine(f.ProductId, ContainerState.Full, 20)), true);

        Assert.Equal(0, await f.Stock());
        var bidon = Assert.Single(dto.Products, p => p.ProductId == f.ProductId);
        Assert.Equal((20, 0), (bidon.FullReturned, bidon.FullMissing));
        var correccion = Assert.Single(dto.Corrections);
        Assert.Equal("Se contó mal", correccion.Reason);
        Assert.Equal(f.AdminId.ToString("N"), correccion.CorrectedByUserName);
        var linea = Assert.Single(correccion.Lines);
        Assert.Equal((ContainerState.Full, 18, 20), (linea.State, linea.PreviousQuantity, linea.NewQuantity));

        // La descarga original sigue ahi: la correccion es un movimiento mas.
        var descargas = await f.Read(db => db.SessionStockMovements
            .Where(m => m.DeliverySessionId == f.SessionId && m.ProductId == f.ProductId
                && m.Type == SessionStockMovementType.ReturnedAtClose)
            .OrderBy(m => m.Quantity).Select(m => m.Quantity).ToListAsync());
        Assert.Equal([-18, -2], descargas);

        // El resumen diario cuenta la descarga corregida.
        var opened = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        var daily = await f.Send(new GetDailySummaryQuery(f.VehicleId, DateOnly.FromDateTime(opened.AddHours(-3))), true);
        var fila = Assert.Single(Assert.Single(daily.Sessions).Products, p => p.ProductId == f.ProductId);
        Assert.Equal((20, 0), (fila.Stock.FullReturned, fila.Stock.FullDifference));
    }

    [Fact]
    public async Task Corregir_hacia_abajo_y_con_vacios_que_no_se_habian_anotado()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(3, returned: 3));
        await f.Send(f.Close(full: 17), true);

        await f.Send(Correct(f, returns:
        [
            new CorrectReturnLine(f.ProductId, ContainerState.Full, 16),
            new CorrectReturnLine(f.ProductId, ContainerState.Empty, 3),
        ]), true);

        Assert.Equal(1, await f.Stock());
        Assert.Equal(0, await f.Stock(ContainerState.Empty));
    }

    [Fact]
    public async Task Corregir_el_kilometraje_de_la_ultima_salida_mueve_el_del_vehiculo()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true);

        var dto = await f.Send(Correct(f, km: 115), true);

        Assert.Equal(115, dto.KilometersAtClose);
        Assert.Equal((110, 115), (dto.Corrections[0].PreviousKilometers, dto.Corrections[0].NewKilometers));
        Assert.Equal(115, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));

        var menor = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Correct(f, km: 99), true));
        Assert.Contains("menor al de salida", menor.Message);
    }

    [Fact]
    public async Task Si_el_vehiculo_volvio_a_salir_el_kilometraje_no_puede_pasarlo()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true);
        var primera = f.SessionId;
        await f.Load();
        var segunda = await f.Send(new OpenSessionCommand(f.ZoneId, 120));

        f.SessionId = primera;
        var error = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Correct(f, km: 130), true));
        Assert.Contains("120 km", error.Message);

        await f.Send(Correct(f, km: 118), true);
        // El vehiculo ya tiene el kilometraje de la salida nueva: no se toca.
        Assert.Equal(120, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    public async Task Corregir_la_plata_rendida_cambia_la_diferencia_y_guarda_el_monto_anterior()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(2, paid: true));
        await f.Send(f.Close(full: 18), true);

        var sinRendir = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Correct(f, amount: 200), true));
        Assert.Contains("todavía no se rindió", sinRendir.Message);

        await f.Send(new RegisterSettlementCommand(f.SessionId, 150, null), true);
        var dto = await f.Send(Correct(f, amount: 200), true);

        Assert.Equal(200, dto.AmountReceived);
        Assert.Equal((150m, 200m), (dto.Corrections[0].PreviousAmountReceived, dto.Corrections[0].NewAmountReceived));
        var liquidacion = await f.Send(new GetSessionSettlementQuery(f.SessionId), true);
        Assert.Equal((200m, 0m), (liquidacion.AmountReceived, liquidacion.CashDifference));
    }

    [Fact]
    public async Task No_se_corrige_una_salida_abierta_ni_sin_cambios_ni_un_producto_ajeno()
    {
        var f = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Correct(f, km: 120), true));

        await f.Send(f.Close(), true);
        var igual = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Correct(f, km: 110,
            returns: new CorrectReturnLine(f.ProductId, ContainerState.Full, 20)), true));
        Assert.Contains("nada para corregir", igual.Message);

        var ajeno = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Correct(f,
            returns: new CorrectReturnLine(Guid.NewGuid(), ContainerState.Full, 1)), true));
        Assert.Contains("no anduvo en esta salida", ajeno.Message);

        Assert.Equal(0, await f.Read(db => db.SessionReceptionCorrections.CountAsync(c => c.DeliverySessionId == f.SessionId)));
    }

    [Fact]
    public async Task El_chofer_no_corrige_y_el_motivo_es_obligatorio()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(Correct(f, km: 115)));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(Correct(f, km: 115) with { Reason = "  " }, true));
        Assert.Equal(110, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId)
            .Select(s => s.KilometersAtClose).SingleAsync()));
    }
}
