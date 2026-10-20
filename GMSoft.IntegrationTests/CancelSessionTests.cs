using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.Cancel;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.GetStatus;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// La oficina anula una salida abierta que no tuvo movimientos (la abrió y el chofer no
/// vino): la carga vuelve a quedar pendiente y el camión puede salir de nuevo.
/// </summary>
[Collection("PostgreSQL")]
public class CancelSessionTests(PostgresFixture fixture)
{
    private static CancelSessionCommand Cancel(Scenario f) => new(f.SessionId, " El chofer no vino ");

    private Task<int> PendingLoads(Scenario f) => f.Read(db =>
        db.VehicleLoads.CountAsync(l => l.VehicleId == f.VehicleId && l.ConsumedBySessionId == null));

    [Fact]
    public async Task Anular_una_salida_sin_movimientos_devuelve_la_carga_y_deja_volver_a_salir()
    {
        var f = await new Scenario(fixture).Seed();
        Assert.Equal(0, await PendingLoads(f));

        await f.Send(Cancel(f), true);

        // El chofer se entera: su salida figura anulada.
        var status = await f.Send(new GetSessionStatusQuery(f.SessionId));
        Assert.Equal(SessionStatus.Cancelled, status.Status);
        // Para todo lo demás queda oculta.
        await Assert.ThrowsAsync<NotFoundException>(() => f.Send(new GetSessionByIdQuery(f.SessionId), true));
        var lista = await f.Send(new GetSessionsQuery(VehicleId: f.VehicleId), true);
        Assert.Empty(lista.Items);
        // Queda el registro de quién, cuándo y por qué.
        var anulada = await f.Read(db => db.DeliverySessions.IgnoreQueryFilters().SingleAsync(s => s.Id == f.SessionId));
        Assert.Equal(("El chofer no vino", f.AdminId), (anulada.CancellationReason, anulada.CancelledByUserId));
        Assert.NotNull(anulada.CancelledAt);

        // La carga volvió a estar pendiente y el chofer puede abrir otra salida.
        Assert.Equal(3, await PendingLoads(f));
        var nueva = await f.Send(new OpenSessionCommand(f.ZoneId, 100));
        Assert.NotEqual(f.SessionId, nueva);
    }

    [Fact]
    public async Task El_camion_vuelve_al_kilometraje_de_su_ultima_vuelta()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true); // volvió con 110 km
        await f.Load();
        f.SessionId = await f.Send(new OpenSessionCommand(f.ZoneId, 150));

        await f.Send(Cancel(f), true);

        Assert.Equal(110, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
    }

    [Fact]
    public async Task Con_una_visita_registrada_no_se_puede_anular()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(1));

        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Send(Cancel(f), true));
        Assert.Contains("movimientos", error.Message);
        Assert.Equal(SessionStatus.Open, (await f.Send(new GetSessionByIdQuery(f.SessionId), true)).Status);
    }

    [Fact]
    public async Task Con_una_recarga_tampoco()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Restock(), true);

        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Cancel(f), true));
    }

    [Fact]
    public async Task El_chofer_no_puede_y_hace_falta_motivo_y_la_salida_abierta()
    {
        var f = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(Cancel(f)));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(Cancel(f) with { Reason = "  " }, true));

        await f.Send(f.Close(), true);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Cancel(f), true));
    }
}
