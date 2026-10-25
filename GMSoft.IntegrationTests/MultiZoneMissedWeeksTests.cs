using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class MultiZoneMissedWeeksTests(PostgresFixture fixture)
{
    [Fact]
    public async Task A_closed_two_zone_departure_counts_a_missed_week_for_customers_of_both_zones()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        var segunda = new Zone { Id = Guid.NewGuid(), Name = "Segunda " + Guid.NewGuid() };
        var deLaSegunda = f.Customer(1, [1]); deLaSegunda.ZoneId = segunda.Id;
        var salida = DateTime.UtcNow.AddDays(-1);
        await f.Write(db =>
        {
            db.Zones.Add(segunda);
            db.Customers.Add(deLaSegunda);
            var s = new DeliverySession { Id = Guid.NewGuid(), VehicleId = f.VehicleId, DriverId = f.DriverId, ZoneId = f.ZoneId,
                OpenedAt = salida, ClosedAt = salida.AddHours(8), Status = SessionStatus.Closed, KilometersAtOpen = 100, RouteDays = [1] };
            s.Zones.Add(new DeliverySessionZone { ZoneId = f.ZoneId, Position = 0 });
            s.Zones.Add(new DeliverySessionZone { ZoneId = segunda.Id, Position = 1 });
            db.DeliverySessions.Add(s);
            return Task.CompletedTask;
        });
        // El alta pone la fecha de hoy: se corre después para que los dos existieran antes de la salida.
        await f.Write(async db =>
        {
            (await db.Customers.FindAsync(f.CustomerId))!.CreatedAt = salida.AddDays(-30);
            (await db.Customers.FindAsync(deLaSegunda.Id))!.CreatedAt = salida.AddDays(-30);
        });

        var clientes = (await f.Send(new GetCustomersQuery(VehicleId: f.VehicleId), true)).Items;
        // El camión pasó por las dos zonas y ninguno compró: los dos perdieron el turno.
        Assert.Equal(1, Assert.Single(clientes, c => c.Id == f.CustomerId).WeeksWithoutPurchase);
        Assert.Equal(1, Assert.Single(clientes, c => c.Id == deLaSegunda.Id).WeeksWithoutPurchase);
    }
}
