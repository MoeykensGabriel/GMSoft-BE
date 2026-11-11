using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.ActiveDepartures;
using GMSoft.Domain.Enums;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class RouteProgressTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Salidas")]
    public async Task Home_progress_counts_the_same_route_sheet_the_driver_sees()
    {
        var f = await new Scenario(fixture).Seed();
        var noCompro = f.Customer(8); var volvio = f.Customer(9); var pendiente = f.Customer(10);
        var otroDia = f.Customer(11, [2]); var inactivo = f.Customer(12); inactivo.IsActive = false;
        var sinCamion = f.Customer(13); sinCamion.VehicleId = null;
        await f.Write(db => { db.Customers.AddRange(noCompro, volvio, pendiente, otroDia, inactivo, sinCamion); return Task.CompletedTask; });

        await f.Send(f.Sale());
        await f.Send(NoPurchase(noCompro.Id));
        // "No compró" y después volvió y compró: cuenta como atendido.
        await f.Send(NoPurchase(volvio.Id));
        await f.Send(f.Sale() with { CustomerId = volvio.Id, ClientRequestId = Guid.NewGuid() });

        var departure = Assert.Single(await f.Send(new GetActiveDeparturesQuery(), true), d => d.SessionId == f.SessionId);
        var hojaDelChofer = await f.Send(new GetCustomersQuery(ZoneId: f.ZoneId, PageSize: 100));
        Assert.Equal(hojaDelChofer.TotalCount, departure.RouteProgress.Total);
        Assert.Equal(new RouteProgressDto(4, 2, 1), departure.RouteProgress);
    }

    private static RegisterDeliveryCommand NoPurchase(Guid customerId) => new(customerId, null,
        DeliveryType.NoPurchase, [], [], [], null, null, Guid.NewGuid(), NoPurchaseReason.NotHome);
}
