using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class RoutePlanningTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Moving_two_customers_appends_in_request_order_and_returns_notes()
    {
        var f = await new Scenario(fixture).Seed();
        var destination = new Zone { Id = Guid.NewGuid(), Name = "Zona destino" };
        var second = f.Customer(12);
        second.Notes = "Código sistema anterior: 12345. En el sistema viejo figuraba en Zona destino.";
        var existing = f.Customer(30);
        existing.ZoneId = destination.Id;
        await f.Write(db =>
        {
            db.Zones.Add(destination);
            db.Customers.AddRange(second, existing);
            return Task.CompletedTask;
        });

        var original = await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true);
        Assert.Equal(second.Notes, original.Items.Single(c => c.Id == second.Id).Notes);
        var saved = await f.Send(new SaveRoutePlanningCommand(f.VehicleId, f.ZoneId, 1, original.Version,
            [second.Id, f.CustomerId],
            [new(second.Id, ZoneId: destination.Id), new(f.CustomerId, ZoneId: destination.Id)]), true);

        Assert.Empty(saved.Items);
        Assert.Empty((await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true)).Items);
        var moved = await f.Send(new GetRoutePlanningQuery(f.VehicleId, destination.Id, 1), true);
        Assert.Equal(new[] { existing.Id, second.Id, f.CustomerId }, moved.Items.Select(c => c.Id));
        Assert.Equal(new[] { 30, 31, 32 }, moved.Items.Select(c => c.RouteOrder));
        Assert.All(moved.Items, c => Assert.Equal(destination.Id, c.ZoneId));
        Assert.Equal(second.Notes, moved.Items.Single(c => c.Id == second.Id).Notes);
        Assert.Equal(2, await f.Read(db => db.Customers.CountAsync(c =>
            (c.Id == second.Id || c.Id == f.CustomerId) && c.ZoneId == destination.Id)));
    }
}
