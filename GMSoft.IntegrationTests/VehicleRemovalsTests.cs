using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.Removal;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleRemovalsTests(PostgresFixture fixture)
{
    private static RequestRemoval Request(Guid customerId) =>
        new(customerId, Guid.NewGuid(), RemovalReason.Moved, "Otra ciudad", false);

    [Fact]
    public async Task List_filters_by_vehicle_and_confirm_all_resolves_only_that_vehicle()
    {
        var a = await new Scenario(fixture).Seed();
        var b = await new Scenario(fixture).Seed();
        var segundo = a.Customer(8);
        await a.Write(db => { db.Customers.Add(segundo); return Task.CompletedTask; });
        await a.Send(Request(a.CustomerId));
        await a.Send(Request(segundo.Id));
        var deOtro = await b.Send(Request(b.CustomerId));

        var lista = await a.Send(new ListRemovals(VehicleId: a.VehicleId), true);
        Assert.Equal(2, lista.TotalCount);
        Assert.All(lista.Items, r => Assert.Equal(a.VehicleId, r.VehicleId));

        // Si la oficina veía otra cantidad, no se confirma ninguna.
        await Assert.ThrowsAsync<ConflictException>(() => a.Send(new ConfirmVehicleRemovals(a.VehicleId, 1), true));
        Assert.Equal(2, (await a.Send(new ListRemovals(VehicleId: a.VehicleId), true)).TotalCount);

        Assert.Equal(2, await a.Send(new ConfirmVehicleRemovals(a.VehicleId, 2), true));
        Assert.Equal(0, (await a.Send(new ListRemovals(VehicleId: a.VehicleId), true)).TotalCount);
        var activos = await a.Read(db => db.Customers.Where(c => c.Id == a.CustomerId || c.Id == segundo.Id)
            .Select(c => c.IsActive).ToListAsync());
        Assert.All(activos, Assert.False);
        var confirmadas = await a.Read(db => db.Set<CustomerRemovalRequest>()
            .Where(r => r.VehicleId == a.VehicleId).Select(r => new { r.Status, r.ResolvedByUserId }).ToListAsync());
        Assert.All(confirmadas, r => { Assert.Equal(RemovalStatus.Confirmed, r.Status); Assert.Equal(a.AdminId, r.ResolvedByUserId); });

        // La del otro camión sigue pendiente y su cliente activo.
        Assert.Equal(RemovalStatus.Pending, (await b.Read(db => db.Set<CustomerRemovalRequest>()
            .Where(r => r.Id == deOtro.Id).Select(r => r.Status).SingleAsync())));
        Assert.True(await b.Read(db => db.Customers.Where(c => c.Id == b.CustomerId).Select(c => c.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Only_admin_confirms_all()
    {
        var a = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<ForbiddenException>(() => a.Send(new ConfirmVehicleRemovals(a.VehicleId, 0)));
    }
}
