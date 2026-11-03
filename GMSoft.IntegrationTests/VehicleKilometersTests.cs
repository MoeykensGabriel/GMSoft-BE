using System.Reflection;
using GMSoft.API.Controllers;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Reports.VehicleKilometers;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleKilometersTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Only_closed_trips_in_range_count_with_their_zones_in_order()
    {
        var a = await new Scenario(fixture).Seed(open: false);
        var second = Guid.NewGuid();
        var start = new DateTime(2032, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var oneZone = Trip(a, start, SessionStatus.Closed, 100, 140);
        var twoZones = Trip(a, start.AddDays(1), SessionStatus.Closed, 150, 210);
        var open = Trip(a, start.AddDays(2), SessionStatus.Open, 210, null);
        var cancelled = Trip(a, start.AddDays(2).AddHours(1), SessionStatus.Cancelled, 210, 900);
        var before = Trip(a, start.AddDays(-1), SessionStatus.Closed, 0, 5000);
        var after = Trip(a, start.AddDays(3), SessionStatus.Closed, 0, 5000);
        await a.Write(db =>
        {
            db.Zones.Add(new Zone { Id = second, Name = "Segunda " + second });
            db.DeliverySessions.AddRange(oneZone, twoZones, open, cancelled, before, after);
            db.DeliverySessionZones.AddRange(
                new DeliverySessionZone { DeliverySessionId = oneZone.Id, ZoneId = a.ZoneId, Position = 0 },
                new DeliverySessionZone { DeliverySessionId = twoZones.Id, ZoneId = a.ZoneId, Position = 0 },
                new DeliverySessionZone { DeliverySessionId = twoZones.Id, ZoneId = second, Position = 1 });
            return Task.CompletedTask;
        });

        var report = await a.Send(new GetVehicleKilometersQuery(new(2032, 3, 8), new(2032, 3, 10)), admin: true);

        var v = Assert.Single(report.Vehicles, x => x.VehicleId == a.VehicleId);
        Assert.Equal(2, v.Sessions);
        Assert.Equal(100, v.TotalKilometers);
        Assert.Equal(50m, v.AveragePerSession);
        Assert.Equal(10, v.KilometersBetweenSessions);
        Assert.Equal(2, v.DaysWorked);
        Assert.Equal(["Zona " + a.ZoneId, $"Zona {a.ZoneId} → Segunda {second}"],
            v.Routes.Select(r => r.Route).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_vehicle_without_closed_trips_is_left_out()
    {
        var idle = await new Scenario(fixture).Seed(open: false);
        var report = await idle.Send(new GetVehicleKilometersQuery(new(2032, 3, 8), new(2032, 3, 10)), admin: true);
        Assert.DoesNotContain(report.Vehicles, v => v.VehicleId == idle.VehicleId);
    }

    [Fact]
    public async Task Only_admin_can_read_it_and_the_range_is_validated()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new GetVehicleKilometersQuery(new(2032, 3, 8), new(2032, 3, 10))));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(
            new GetVehicleKilometersQuery(new(2032, 3, 10), new(2032, 3, 8)), admin: true));
        Assert.Equal("vehicle-kilometers", typeof(ReportsController).GetMethod("VehicleKilometers")!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    private static DeliverySession Trip(Scenario s, DateTime opened, SessionStatus status, int kmOpen, int? kmClose) => new()
    {
        Id = Guid.NewGuid(), VehicleId = s.VehicleId, DriverId = s.DriverId, ZoneId = s.ZoneId,
        OpenedAt = opened, ClosedAt = status == SessionStatus.Open ? null : opened.AddHours(8),
        Status = status, KilometersAtOpen = kmOpen, KilometersAtClose = kmClose
    };
}
