using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.VehicleLoads.Clear;
using GMSoft.Application.Features.VehicleLoads.GetPending;
using GMSoft.Application.Features.VehicleLoads.Register;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class ClearVehicleLoadTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Carga")]
    public async Task Clear_removes_every_pending_load_and_records_who_did_it()
    {
        var f = await new Scenario(fixture).Seed(false);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 5)], [1], Guid.NewGuid()), true);
        await f.Send(new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 3)], [1], Guid.NewGuid()), true);

        Assert.Equal(2, await f.Send(new ClearVehicleLoadCommand(f.VehicleId), true));

        Assert.Empty((await f.Send(new GetPendingVehicleLoadQuery(f.VehicleId), true)));
        var removed = await f.Read(db => db.VehicleLoads.IgnoreQueryFilters()
            .Where(l => l.VehicleId == f.VehicleId).ToListAsync());
        Assert.Equal(2, removed.Count);
        Assert.All(removed, l =>
        {
            Assert.True(l.IsDeleted);
            Assert.NotNull(l.DeletedAt);
            Assert.Equal(f.AdminId, l.RemovedByUserId);
        });

        // Vaciar un camión ya vacío no es un error, y sin carga no se puede salir.
        Assert.Equal(0, await f.Send(new ClearVehicleLoadCommand(f.VehicleId), true));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send(new OpenSessionCommand(f.ZoneId, 100)));
    }

    [Fact, Trait("Grupo", "Carga")]
    public async Task Clear_does_not_touch_the_load_that_already_left()
    {
        var f = await new Scenario(fixture).Seed();

        Assert.Equal(0, await f.Send(new ClearVehicleLoadCommand(f.VehicleId), true));

        Assert.True(await f.Read(db => db.VehicleLoads.AnyAsync(l => l.VehicleId == f.VehicleId && l.ConsumedBySessionId != null)));
        Assert.Equal(20, await f.Stock());
    }
}
