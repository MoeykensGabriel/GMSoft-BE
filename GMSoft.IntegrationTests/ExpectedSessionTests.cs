using GMSoft.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// El celular reenvía visitas guardadas. Una que nunca llegó no tiene que caer en otra salida;
/// una que ya llegó se devuelve igual aunque su salida ya no sea la abierta.
/// </summary>
[Collection("PostgreSQL")]
public class ExpectedSessionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Una_visita_cargada_en_otra_salida_no_se_registra_en_la_abierta()
    {
        var f = await new Scenario(fixture).Seed();
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            f.Send(f.Sale(1) with { ExpectedSessionId = Guid.NewGuid() }));
        Assert.Contains("ya se cerró", error.Message);
        Assert.Equal(0, await f.Sales());
    }

    [Fact]
    public async Task Con_la_salida_abierta_se_registra_y_el_reintento_devuelve_la_misma()
    {
        var f = await new Scenario(fixture).Seed();
        var venta = f.Sale(1) with { ExpectedSessionId = f.SessionId };
        var primera = await f.Send(venta);
        // El reintento busca la clave antes de mirar la salida: aunque ya no coincida, devuelve la original.
        var reintento = await f.Send(venta with { ExpectedSessionId = Guid.NewGuid() });
        Assert.Equal(primera.DeliveryId, reintento.DeliveryId);
        Assert.Equal(1, await f.Sales());
        Assert.Equal(f.SessionId, await f.Read(db => db.Deliveries.Where(d => d.Id == primera.DeliveryId)
            .Select(d => d.DeliverySessionId).SingleAsync()));
    }
}
