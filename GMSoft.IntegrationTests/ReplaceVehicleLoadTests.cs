using GMSoft.Application.Features.VehicleLoads.GetPending;
using GMSoft.Application.Features.VehicleLoads.Register;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class ReplaceVehicleLoadTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Carga")]
    public async Task Replacing_leaves_exactly_the_new_load_and_records_the_old_one_as_removed()
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 10)], [1], Guid.NewGuid()), true);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.NoneId, 4)], [1], Guid.NewGuid()), true);

        // Baja el bidón de 10 a 6, saca el otro producto y cambia el día.
        var pedido = Guid.NewGuid();
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 6)], [2], pedido, ReplacePending: true), true);
        // Un reintento del mismo pedido no vuelve a bajar ni a cargar.
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 6)], [2], pedido, ReplacePending: true), true);

        var arriba = await f.Send(new GetPendingVehicleLoadQuery(f.VehicleId), true);
        var linea = Assert.Single(arriba);
        Assert.Equal(f.ProductId, linea.ProductId);
        Assert.Equal(6, linea.Quantity);
        Assert.Equal(new[] { 2 }, linea.RouteDays);

        var bajadas = await f.Read(db => db.VehicleLoads.IgnoreQueryFilters()
            .Where(l => l.VehicleId == f.VehicleId && l.IsDeleted).Select(l => new { l.Quantity, l.RemovedByUserId }).ToListAsync());
        Assert.Equal(new[] { 4, 10 }, bajadas.Select(b => b.Quantity).Order());
        Assert.All(bajadas, b => Assert.Equal(f.AdminId, b.RemovedByUserId));
    }

    [Fact, Trait("Grupo", "Carga")]
    public async Task Without_replace_it_still_adds_on_top()
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 10)], [1], Guid.NewGuid()), true);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 2)], [1], Guid.NewGuid()), true);
        Assert.Equal(12, (await f.Send(new GetPendingVehicleLoadQuery(f.VehicleId), true)).Sum(l => l.Quantity));
    }
}
