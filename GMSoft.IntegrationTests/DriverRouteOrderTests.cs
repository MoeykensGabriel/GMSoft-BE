using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.RouteOrder;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DriverRouteOrderTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Multi_day_route_includes_visited_and_preserves_other_trucks_and_records_author()
    {
        var f = await new Scenario(fixture).Seed();
        var second = f.Customer(12, [2]);
        var bothDays = f.Customer(15, [1, 2]);
        var otherDay = f.Customer(18, [3]);
        var inactive = f.Customer(20); inactive.IsActive = false;
        var otherTruck = f.Customer(9); otherTruck.VehicleId = Guid.NewGuid();
        await f.Write(async db =>
        {
            db.Vehicles.Add(new Vehicle { Id = otherTruck.VehicleId.Value, Name = "Otro", LicensePlate = Guid.NewGuid().ToString("N")[..12], Type = VehicleType.Truck });
            db.Customers.AddRange(second, bothDays, otherDay, inactive, otherTruck);
            (await db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId)).RouteDays = [1, 2];
        });
        await f.Send(f.Sale());
        var original = await f.Send(new GetDriverRouteOrderQuery(f.SessionId));
        Assert.Equal(new[] { f.CustomerId, second.Id, bothDays.Id }, original.Items.Select(c => c.Id));
        var result = await f.Send(new SaveDriverRouteOrderCommand(f.SessionId,
            [bothDays.Id, f.CustomerId, second.Id], original.Version));
        Assert.Equal(new[] { 7, 12, 15 }, result.Items.Select(c => c.RouteOrder));
        Assert.Equal(new[] { bothDays.Id, f.CustomerId, second.Id }, result.Items.Select(c => c.Id));
        Assert.Equal(9, await f.Read(db => db.Customers.Where(c => c.Id == otherTruck.Id).Select(c => c.RouteOrder).SingleAsync()));
        var history = await f.Read(db => db.RouteOrderChanges.Where(c => c.DeliverySessionId == f.SessionId).ToListAsync());
        var change = Assert.Single(history);
        Assert.Equal(f.UserId, change.ChangedByUserId);
        Assert.Equal(3, change.MovedCount);
        var office = await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true);
        Assert.Equal(f.UserId.ToString("N"), office.LastChange!.ChangedByUserName);
        Assert.Equal(3, office.LastChange.MovedCount);
        var detail = await f.Send(new GetSessionByIdQuery(f.SessionId), true);
        Assert.Equal(office.LastChange, detail.DriverRouteOrderChange);
        Assert.Equal(result.Version, (await f.Send(new GetDriverRouteOrderQuery(f.SessionId))).Version);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public async Task Invalid_snapshot_does_not_write_or_record(string kind)
    {
        var f = await new Scenario(fixture).Seed();
        var second = f.Customer(12);
        await f.Write(db => { db.Customers.Add(second); return Task.CompletedTask; });
        var original = await f.Send(new GetDriverRouteOrderQuery(f.SessionId));
        var ids = kind switch
        {
            "foreign" => new[] { f.CustomerId, (await new Scenario(fixture).Seed()).CustomerId },
            "duplicate" => new[] { f.CustomerId, f.CustomerId },
            "missing" => new[] { f.CustomerId },
            _ => new[] { second.Id, f.CustomerId }
        };
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new SaveDriverRouteOrderCommand(f.SessionId, ids,
            kind == "version" ? "old" : original.Version)));
        Assert.Equal(original.Version, (await f.Send(new GetDriverRouteOrderQuery(f.SessionId))).Version);
        Assert.Equal(0, await f.Read(db => db.RouteOrderChanges.CountAsync(c => c.DeliverySessionId == f.SessionId)));
    }

    [Fact]
    public async Task Another_driver_cannot_read_or_write_and_closed_session_rejects_both()
    {
        var f = await new Scenario(fixture).Seed();
        var other = await new Scenario(fixture).Seed();
        var original = await f.Send(new GetDriverRouteOrderQuery(f.SessionId));
        var save = new SaveDriverRouteOrderCommand(f.SessionId, [f.CustomerId], original.Version);
        await Assert.ThrowsAsync<ForbiddenException>(() => other.Send(new GetDriverRouteOrderQuery(f.SessionId)));
        await Assert.ThrowsAsync<ForbiddenException>(() => other.Send(save));
        await f.Send(f.Close(), true);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new GetDriverRouteOrderQuery(f.SessionId)));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(save));
    }

    [Fact]
    public async Task Office_changes_are_audited_and_invalidate_driver_version()
    {
        var f = await new Scenario(fixture).Seed();
        var second = f.Customer(12);
        await f.Write(db => { db.Customers.Add(second); return Task.CompletedTask; });
        var driver = await f.Send(new GetDriverRouteOrderQuery(f.SessionId));
        var office = await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true);
        Assert.Equal(driver.Version, office.Version);
        Assert.Null(office.LastChange);
        var saved = await f.Send(new SaveRoutePlanningCommand(f.VehicleId, f.ZoneId, 1, office.Version,
            [second.Id, f.CustomerId], []), true);
        Assert.Equal(f.AdminId.ToString("N"), saved.LastChange!.ChangedByUserName);
        Assert.Equal(2, saved.LastChange.MovedCount);
        Assert.Null((await f.Send(new GetSessionByIdQuery(f.SessionId), true)).DriverRouteOrderChange);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new SaveDriverRouteOrderCommand(f.SessionId,
            [f.CustomerId, second.Id], driver.Version)));
    }

    [Fact]
    public async Task Saving_unchanged_route_does_not_create_a_change()
    {
        var f = await new Scenario(fixture).Seed();
        var original = await f.Send(new GetDriverRouteOrderQuery(f.SessionId));
        await f.Send(new SaveDriverRouteOrderCommand(f.SessionId, [f.CustomerId], original.Version));
        Assert.Null((await f.Send(new GetRoutePlanningQuery(f.VehicleId, f.ZoneId, 1), true)).LastChange);
    }
}
