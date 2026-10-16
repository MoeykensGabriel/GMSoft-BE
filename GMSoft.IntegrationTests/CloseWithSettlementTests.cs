using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// Recibir el camion y rendir la plata en un solo paso: o queda todo guardado o nada.
/// Sin plata, la salida queda marcada como pendiente de rendir.
/// </summary>
[Collection("PostgreSQL")]
public class CloseWithSettlementTests(PostgresFixture fixture)
{
    private Task<int> Settlements(Scenario f) =>
        f.Read(db => db.SessionCashSettlements.CountAsync(s => s.DeliverySessionId == f.SessionId));

    [Fact]
    public async Task Recibir_con_la_plata_cierra_y_rinde_juntos()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(2, paid: true));

        var result = await f.Send(f.Close(full: 18) with { Settlement = new(150, " Faltó un billete ") }, true);

        Assert.Equal(50, result.CashDifference);
        var liquidacion = await f.Send(new GetSessionSettlementQuery(f.SessionId), true);
        Assert.Equal((150m, 50m, "Faltó un billete"), (liquidacion.AmountReceived!.Value, liquidacion.CashDifference!.Value, liquidacion.Notes));
        Assert.True((await f.Send(new GetSessionByIdQuery(f.SessionId), true)).IsSettled);

        // Ya rendida: la rendicion aparte se rechaza y se corrige desde la recepcion.
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new RegisterSettlementCommand(f.SessionId, 200, null), true));
    }

    [Fact]
    public async Task Sin_la_plata_queda_pendiente_de_rendir_y_se_rinde_despues()
    {
        var f = await new Scenario(fixture).Seed();
        var result = await f.Send(f.Close(), true);

        Assert.Null(result.CashDifference);
        Assert.Equal(0, await Settlements(f));
        var opened = await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.OpenedAt).SingleAsync());
        var lista = await f.Send(new GetSessionsQuery(VehicleId: f.VehicleId, Date: DateOnly.FromDateTime(opened.AddHours(-3))), true);
        Assert.False(Assert.Single(lista.Items).IsSettled);

        await f.Send(new RegisterSettlementCommand(f.SessionId, 0, null), true);
        Assert.True((await f.Send(new GetSessionByIdQuery(f.SessionId), true)).IsSettled);
    }

    [Fact]
    public async Task Si_algo_falla_no_se_guarda_ni_la_recepcion_ni_la_plata()
    {
        var f = await new Scenario(fixture).Seed();

        await Assert.ThrowsAsync<ValidationException>(() => f.Send(f.Close() with { Settlement = new(-1, null) }, true));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(f.Close() with { KilometersAtClose = 50, Settlement = new(100, null) }, true));

        Assert.Equal(0, await Settlements(f));
        Assert.Equal(SessionStatus.Open, await f.Read(db => db.DeliverySessions.Where(s => s.Id == f.SessionId).Select(s => s.Status).SingleAsync()));
    }
}
