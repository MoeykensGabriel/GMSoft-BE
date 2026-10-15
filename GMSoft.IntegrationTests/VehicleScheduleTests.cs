using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Vehicles.Schedule;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleScheduleTests(PostgresFixture fixture)
{
    private static ScheduleDayInput[] Week(Guid zone) => Enumerable.Range(1, 7).Select(d => new ScheduleDayInput(d, d is 1 or 4 ? zone : null)).ToArray();
    [Fact]
    public async Task Concurrent_first_saves_leave_one_complete_week()
    {
        var s = await new Scenario(fixture).Seed(false);
        var on = Week(s.ZoneId);
        var off = on.Select(d => d with { ZoneId = null }).ToArray();
        await Task.WhenAll(s.Send(new SaveVehicleScheduleCommand(s.VehicleId, on), true),
            s.Send(new SaveVehicleScheduleCommand(s.VehicleId, off), true));
        var result = await s.Send(new GetVehicleScheduleQuery(s.VehicleId), true);
        Assert.Equal(7, result.Days.Count);
        Assert.Equal(result.Days[0].ZoneId, result.Days[3].ZoneId);
        Assert.All(result.Days.Where(d => d.Day is not (1 or 4)), d => Assert.Null(d.ZoneId));
    }

    [Fact]
    public void Http_contract_has_expected_routes_and_roles_without_starting_api()
    {
        var controller = typeof(GMSoft.API.Controllers.VehicleScheduleController);
        Assert.Equal("Admin,Driver", ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)
            Attribute.GetCustomAttribute(controller, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!).Roles);
        foreach (var name in new[] { "Save", "Coverage", "Uncovered" })
            Assert.Equal("Admin", ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)
                Attribute.GetCustomAttribute(controller.GetMethod(name)!, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!).Roles);
        Assert.Equal("api/sessions/suggested-zone", ((Microsoft.AspNetCore.Mvc.HttpGetAttribute)
            Attribute.GetCustomAttribute(controller.GetMethod("Suggest")!, typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute))!).Template);
        var oldChange = System.Text.Json.JsonSerializer.Deserialize<RouteCustomerChange>(
            "{\"id\":\"11111111-1111-4111-8111-111111111111\",\"visitDays\":[1]}",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Null(oldChange.ZoneId);
    }
    [Fact]
    public async Task Save_read_replace_and_all_off_are_persisted()
    {
        var s = await new Scenario(fixture).Seed(false);
        var saved = await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId)), true);
        Assert.True(saved.IsConfigured); Assert.False(saved.IsSuggested); Assert.Equal(7, saved.Days.Count);
        var read = await s.Send(new GetVehicleScheduleQuery(s.VehicleId));
        Assert.Equal(s.ZoneId, read.Days[0].ZoneId); Assert.Null(read.Days[6].ZoneId);
        await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId).Select(d => d with { ZoneId = null }).ToArray()), true);
        read = await s.Send(new GetVehicleScheduleQuery(s.VehicleId), true);
        Assert.True(read.IsConfigured); Assert.All(read.Days, d => Assert.Null(d.ZoneId));
        Assert.Equal(7, await s.Read(db => db.Set<VehicleScheduleDay>().CountAsync(d => d.VehicleId == s.VehicleId)));
    }
    [Fact]
    public async Task Invalid_zone_rolls_back_week_and_inactive_zone_is_rejected()
    {
        var s = await new Scenario(fixture).Seed(false);
        await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId)), true);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(Guid.NewGuid())), true));
        await s.Write(async db => (await db.Zones.SingleAsync(z => z.Id == s.ZoneId)).IsActive = false);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId)), true));
        Assert.Equal(s.ZoneId, (await s.Send(new GetVehicleScheduleQuery(s.VehicleId), true)).Days[0].ZoneId);
    }
    [Fact]
    public async Task Driver_reads_only_assigned_vehicle_and_cannot_write_or_inspect_coverage()
    {
        var s = await new Scenario(fixture).Seed(false);
        var other = await new Scenario(fixture).Seed(false);
        await s.Send(new GetVehicleScheduleQuery(s.VehicleId));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new GetVehicleScheduleQuery(other.VehicleId)));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId))));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new CheckCoverageQuery(s.VehicleId, s.ZoneId, [1])));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new GetUncoveredQuery()));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new SuggestedZoneQuery(other.DriverId)));
    }
    [Fact]
    public async Task History_uses_last_eight_weeks_and_never_saves_suggestion()
    {
        var s = await new Scenario(fixture).Seed(false);
        var now = DateTime.UtcNow;
        var otherZone = Guid.NewGuid();
        await s.Write(db =>
        {
            db.Zones.Add(new Zone { Id = otherZone, Name = "Otra " + otherZone });
            foreach (var ago in new[] { 1, 8, 15 }) db.DeliverySessions.Add(new DeliverySession { DriverId = s.DriverId,
                VehicleId = s.VehicleId, ZoneId = s.ZoneId, OpenedAt = now.AddDays(-ago), RouteDays = [1, 4], Status = SessionStatus.Closed });
            foreach (var ago in new[] { 2, 60, 61, 62, 63 }) db.DeliverySessions.Add(new DeliverySession { DriverId = s.DriverId,
                VehicleId = s.VehicleId, ZoneId = otherZone, OpenedAt = now.AddDays(-ago), RouteDays = [1], Status = SessionStatus.Closed });
            return Task.CompletedTask;
        });
        var result = await s.Send(new GetVehicleScheduleQuery(s.VehicleId), true);
        Assert.False(result.IsConfigured); Assert.True(result.IsSuggested);
        Assert.Equal(s.ZoneId, result.Days[0].ZoneId); Assert.Equal(s.ZoneId, result.Days[3].ZoneId);
        Assert.Equal(0, await s.Read(db => db.Set<VehicleScheduleDay>().CountAsync(d => d.VehicleId == s.VehicleId)));
        Assert.Null((await s.Send(new CheckCoverageQuery(s.VehicleId, s.ZoneId, [1]), true)).Covered);
    }
    [Fact]
    public async Task Suggested_zone_uses_pending_load_days_and_keeps_all_zones_in_day_order()
    {
        var s = await new Scenario(fixture).Seed(false);
        var otherZone = Guid.NewGuid();
        await s.Write(db => { db.Zones.Add(new Zone { Id = otherZone, Name = "Otra " + otherZone }); return Task.CompletedTask; });
        var week = Week(s.ZoneId); week[3] = new(4, otherZone);
        await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, week), true);
        Assert.Empty((await s.Send(new SuggestedZoneQuery())).RouteDays);
        await s.Load();
        await s.Write(async db => { foreach (var load in await db.VehicleLoads.Where(l => l.VehicleId == s.VehicleId).ToListAsync()) load.RouteDays = [4, 1]; });
        var result = await s.Send(new SuggestedZoneQuery());
        Assert.Equal(s.ZoneId, result.ZoneId); Assert.Equal(new[] { 1, 4 }, result.RouteDays);
        Assert.Equal(otherZone, result.Days[1].ZoneId);
        Assert.Equal(result.ZoneId, (await s.Send(new SuggestedZoneQuery(s.DriverId), true)).ZoneId);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Send(new SuggestedZoneQuery(), true));
    }
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task Coverage_matches_saved_week(int day, bool expected)
    {
        var s = await new Scenario(fixture).Seed(false);
        await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId)), true);
        Assert.Equal(expected, (await s.Send(new CheckCoverageQuery(s.VehicleId, s.ZoneId, [day]), true)).Covered);
    }
    [Fact]
    public async Task Uncovered_lists_partial_and_total_mismatch_excludes_unconfigured_and_inactive_and_pages()
    {
        var s = await new Scenario(fixture).Seed(false);
        await s.Send(new SaveVehicleScheduleCommand(s.VehicleId, Week(s.ZoneId)), true);
        var partial = s.Customer(8, [1, 2]); var total = s.Customer(9, [3]); var inactive = s.Customer(10, [3]); inactive.IsActive = false;
        await s.Write(db => { db.Customers.AddRange(partial, total, inactive); return Task.CompletedTask; });
        var result = await s.Send(new GetUncoveredQuery(s.VehicleId), true);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { 2 }, result.Items.Single(c => c.Id == partial.Id).UncoveredDays);
        Assert.Equal(new[] { 3 }, result.Items.Single(c => c.Id == total.Id).UncoveredDays);
        var page = await s.Send(new GetUncoveredQuery(s.VehicleId, 1, 1), true);
        Assert.Single(page.Items); Assert.True(page.HasNextPage);
        var unconfigured = await new Scenario(fixture).Seed(false);
        Assert.Empty((await unconfigured.Send(new GetUncoveredQuery(unconfigured.VehicleId), true)).Items);
    }
    [Fact]
    public async Task Route_zone_changes_append_distinct_positions_and_remove_from_original_route()
    {
        var s = await new Scenario(fixture).Seed(false);
        var zone = Guid.NewGuid(); var second = s.Customer(8); var destination = s.Customer(20); destination.ZoneId = zone;
        await s.Write(db => { db.Zones.Add(new Zone { Id = zone, Name = "Destino" }); db.Customers.AddRange(second, destination); return Task.CompletedTask; });
        var route = await s.Send(new GetRoutePlanningQuery(s.VehicleId, s.ZoneId, 1), true);
        var result = await s.Send(new SaveRoutePlanningCommand(s.VehicleId, s.ZoneId, 1, route.Version,
            route.Items.Select(c => c.Id).ToArray(), [new(s.CustomerId, ZoneId: zone), new(second.Id, ZoneId: zone)]), true);
        Assert.Empty(result.Items);
        var moved = await s.Read(db => db.Customers.Where(c => c.Id == s.CustomerId || c.Id == second.Id).OrderBy(c => c.RouteOrder).ToListAsync());
        Assert.Equal(new[] { 21, 22 }, moved.Select(c => c.RouteOrder)); Assert.All(moved, c => Assert.Equal(zone, c.ZoneId));
    }
}

