using GMSoft.Application.Features.Drivers.GetMe;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DriverVehicleKilometersTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Driver_profile_brings_the_last_kilometers_of_its_truck()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        Assert.Equal(100, (await f.Send(new GetMyDriverProfileQuery())).VehicleKilometers);

        await f.Write(async db => (await db.Vehicles.FindAsync(f.VehicleId))!.CurrentKilometers = 50085);
        Assert.Equal(50085, (await f.Send(new GetMyDriverProfileQuery())).VehicleKilometers);
    }
}
