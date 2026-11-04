using System.Reflection;
using GMSoft.API.Controllers;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Sessions.UsageSince;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class UsageSinceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task The_first_trip_that_was_not_cancelled_sets_the_start()
    {
        // Fechas anteriores a cualquier otro test: la base de pruebas es compartida.
        var f = await new Scenario(fixture).Seed(open: false);
        await f.Write(db =>
        {
            var closed = Trip(f, new DateTime(1990, 3, 5, 12, 0, 0, DateTimeKind.Utc), SessionStatus.Closed);
            db.DeliverySessions.AddRange(
                Trip(f, new DateTime(1990, 3, 1, 12, 0, 0, DateTimeKind.Utc), SessionStatus.Cancelled), closed);
            // Rendida, para no aparecer como pendiente en los tests que listan toda la base.
            db.SessionCashSettlements.Add(new SessionCashSettlement { DeliverySessionId = closed.Id, ReceivedAt = closed.ClosedAt!.Value });
            return Task.CompletedTask;
        });
        var result = await f.Send(new GetUsageSinceQuery(), admin: true);
        Assert.Equal(new DateOnly(1990, 3, 5), result.Since);
        Assert.True(result.Days > 13000);
    }

    [Fact]
    public void Endpoint_is_get_and_admin_only()
    {
        var method = typeof(SessionsController).GetMethod("GetUsageSince")!;
        Assert.Equal("usage-since", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(AppRoles.Admin, method.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    private static DeliverySession Trip(Scenario s, DateTime opened, SessionStatus status) => new()
    {
        Id = Guid.NewGuid(), VehicleId = s.VehicleId, DriverId = s.DriverId, ZoneId = s.ZoneId,
        OpenedAt = opened, ClosedAt = opened.AddHours(8), Status = status, KilometersAtOpen = 0, KilometersAtClose = 10
    };
}
