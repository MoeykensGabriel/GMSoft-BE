using GMSoft.Application.Features.Vehicles.Schedule;
using GMSoft.Domain.Entities;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class ScheduleCustomerCountsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Schedule_counts_active_customers_per_day_and_zone_even_outside_the_agenda()
    {
        var s = await new Scenario(fixture).Seed(open: false);
        var otraZona = new Zone { Id = Guid.NewGuid(), Name = "Otra " + Guid.NewGuid() };
        var lunesYJueves = s.Customer(2, [1, 4]);
        var deOtraZona = s.Customer(3, [1]); deOtraZona.ZoneId = otraZona.Id;
        var inactivo = s.Customer(4, [1]); inactivo.IsActive = false;
        var sinDias = s.Customer(5); sinDias.VisitDays = null;
        await s.Write(db =>
        {
            db.Zones.Add(otraZona);
            db.Customers.AddRange(lunesYJueves, deOtraZona, inactivo, sinDias);
            return Task.CompletedTask;
        });

        var agenda = await s.Send(new GetVehicleScheduleQuery(s.VehicleId), admin: true);

        // El cliente del escenario va los lunes, en la zona del escenario.
        Assert.Equal(new[]
        {
            new ScheduleCustomerCountDto(1, s.ZoneId, "Zona " + s.ZoneId, 2),
            new ScheduleCustomerCountDto(1, otraZona.Id, otraZona.Name, 1),
            new ScheduleCustomerCountDto(4, s.ZoneId, "Zona " + s.ZoneId, 1),
        }, agenda.CustomerCounts);
    }
}
