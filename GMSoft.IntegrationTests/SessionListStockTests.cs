using GMSoft.Application.Features.Sessions.GetList;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class SessionListStockTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Received_sessions_bring_what_was_left_over_and_open_ones_do_not()
    {
        var recibida = await new Scenario(fixture).Seed();
        await recibida.Send(recibida.Sale(3));
        // Salió con 20, entregó 3 y volvieron 15 llenos: faltan 2. Los 3 vacíos no volvieron.
        await recibida.Send(recibida.Close(full: 15), true);
        var abierta = await new Scenario(fixture).Seed();
        await abierta.Send(abierta.Sale(1));

        var cerrada = Assert.Single((await recibida.Send(new GetSessionsQuery(VehicleId: recibida.VehicleId), true)).Items);
        var bidon = Assert.Single(cerrada.Stock, l => l.ProductId == recibida.ProductId);
        Assert.Equal(2, bidon.FullOnBoard);
        Assert.Equal(0, bidon.EmptyOnBoard);
        Assert.All(cerrada.Stock.Where(l => l.ProductId != recibida.ProductId), l => Assert.Equal((0, 0), (l.FullOnBoard, l.EmptyOnBoard)));

        var enLaCalle = Assert.Single((await abierta.Send(new GetSessionsQuery(VehicleId: abierta.VehicleId), true)).Items);
        Assert.Empty(enLaCalle.Stock);
    }
}
