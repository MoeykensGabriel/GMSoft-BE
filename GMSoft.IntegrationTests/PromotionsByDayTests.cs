using GMSoft.Application.Common;
using GMSoft.Application.Features.Promotions.List;
using GMSoft.Application.Features.Promotions.Pending;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class PromotionsByDayTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Promociones")]
    public async Task The_driver_sees_only_the_promotions_of_the_route_days_of_his_departure()
    {
        var f = await new Scenario(fixture).Seed();
        // La salida abierta es del lunes (la carga del escenario es para el lunes).
        var delLunes = await f.Send(new RegisterPromotionCommand(
            new(null, "Del lunes", "123", "Calle 1", null, [1]), [new(f.ProductId, 1)], Guid.NewGuid()));
        var delMartes = await PromotionOfAnotherDeparture(f, 2);

        var pendientes = await f.Send(new GetPendingPromotionsQuery());
        var unica = Assert.Single(pendientes);
        Assert.Equal(delLunes.PromotionId, unica.Id);
        Assert.Equal([1], unica.RouteDays);

        // La oficina filtra por día.
        var martes = await f.Send(new GetPromotionsQuery(VehicleId: f.VehicleId, Day: 2), true);
        Assert.Equal(delMartes, Assert.Single(martes.Items).Id);

        // Una salida que recorre lunes y martes ve las de los dos días.
        await f.Write(async db => (await db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId)).RouteDays = [1, 2]);
        var deDos = await f.Send(new GetPendingPromotionsQuery());
        Assert.Equal(new[] { delLunes.PromotionId, delMartes }.Order(), deDos.Select(p => p.Id).Order());
    }

    [Fact, Trait("Grupo", "Promociones")]
    public async Task Without_an_open_departure_the_driver_sees_the_promotions_of_today()
    {
        var f = await new Scenario(fixture).Seed();
        var hoy = BusinessTime.IsoDayOfWeek(DateTime.UtcNow);
        var otroDia = hoy % 7 + 1;
        await f.Send(new RegisterPromotionCommand(
            new(null, "De otro día", "123", "Calle 1", null, [1]), [new(f.ProductId, 1)], Guid.NewGuid()));
        var deHoy = await PromotionOfAnotherDeparture(f, hoy);
        await f.Write(async db =>
        {
            var salida = await db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId);
            salida.RouteDays = [otroDia];
            salida.Status = SessionStatus.Closed;
            salida.ClosedAt = DateTime.UtcNow;
        });

        Assert.Equal(deHoy, Assert.Single(await f.Send(new GetPendingPromotionsQuery())).Id);
    }

    /// <summary>Una promoción pendiente registrada en otra salida, ya recibida, de ese día de recorrido.</summary>
    private static async Task<Guid> PromotionOfAnotherDeparture(Scenario f, int routeDay)
    {
        var id = Guid.NewGuid();
        await f.Write(db =>
        {
            var salida = new DeliverySession
            {
                Id = Guid.NewGuid(), VehicleId = f.VehicleId, DriverId = f.DriverId, ZoneId = f.ZoneId,
                OpenedAt = DateTime.UtcNow.AddDays(-7), ClosedAt = DateTime.UtcNow.AddDays(-7).AddHours(8),
                Status = SessionStatus.Closed, KilometersAtOpen = 90, RouteDays = [routeDay],
            };
            db.DeliverySessions.Add(salida);
            db.Promotions.Add(new Promotion
            {
                Id = id, ContactName = "De otra salida", Phone = "123", Address = "Calle 2", VisitDays = [routeDay],
                VehicleId = f.VehicleId, DriverId = f.DriverId, DeliverySessionId = salida.Id, ZoneId = f.ZoneId,
                RegisteredAt = salida.OpenedAt, PickupDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = PromotionStatus.Pending, ClientRequestId = Guid.NewGuid(),
            });
            return Task.CompletedTask;
        });
        return id;
    }
}
