using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Reports.VehicleKilometers;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Vehicles.KilometersCheck;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleKilometersCheckTests(PostgresFixture fixture)
{
    /// <summary>Un camión con el km roto (un dígito de más) y sin revisar, cargado para salir.</summary>
    private static async Task<Scenario> Unchecked(PostgresFixture fixture, int systemKm = 30_000_863)
    {
        var s = await new Scenario(fixture).Seed(open: false);
        await s.Write(async db =>
        {
            var v = (await db.Vehicles.FindAsync(s.VehicleId))!;
            v.CurrentKilometers = systemKm;
            v.KilometersCheckPending = true;
        });
        await s.Load();
        return s;
    }

    private static Task<KilometersReadingDto> Pending(Scenario s) => s.Send(new GetPendingKilometersReadingsQuery(), admin: true)
        .ContinueWith(t => t.Result.Single(r => r.VehicleId == s.VehicleId));

    [Fact]
    public async Task The_driver_reads_less_than_the_system_and_the_office_accepts_it()
    {
        var s = await Unchecked(fixture);

        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 187_420));

        var reading = await Pending(s);
        Assert.Equal(187_420, reading.Kilometers);
        Assert.Equal(30_000_863, reading.PreviousKilometers);
        Assert.Equal("Juan Prueba", reading.DriverName);
        Assert.Equal(s.SessionId, reading.SessionId);

        await s.Send(new ResolveKilometersReadingCommand(reading.Id, Accept: true), admin: true);

        var v = await s.Read(db => db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == s.VehicleId));
        Assert.False(v.KilometersCheckPending);
        Assert.Equal(reading.ReadAt, v.KilometersCheckedAt);
        Assert.Equal(187_420, v.CurrentKilometers);
        Assert.DoesNotContain((await s.Send(new GetPendingKilometersReadingsQuery(), admin: true)), r => r.VehicleId == s.VehicleId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Send(new ResolveKilometersReadingCommand(reading.Id, Accept: false), admin: true));
    }

    [Fact]
    public async Task Only_one_reading_may_go_below_and_a_rejection_allows_one_more()
    {
        var s = await Unchecked(fixture);
        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 187_420));
        var v = await s.Read(db => db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == s.VehicleId));
        Assert.False(v.KilometersCheckPending);

        // Sin revisar todavía: la salida siguiente ya no puede bajar ni deja otra lectura.
        await s.Send(new CloseSessionCommand(s.SessionId, 187_500, s.Close().Returns), admin: true);
        await s.Load();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Send(new OpenSessionCommand(s.ZoneId, 187_000)));
        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 187_500));
        Assert.Equal(1, await s.Read(db => db.VehicleKilometersReadings.CountAsync(r => r.VehicleId == s.VehicleId)));

        // La oficina rechaza: la próxima salida lee el tablero otra vez, y esa sí puede bajar.
        await s.Send(new ResolveKilometersReadingCommand((await Pending(s)).Id, Accept: false), admin: true);
        v = await s.Read(db => db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == s.VehicleId));
        Assert.True(v.KilometersCheckPending);
        Assert.Null(v.KilometersCheckedAt);
        await s.Send(new CloseSessionCommand(s.SessionId, 187_540, s.Close().Returns), admin: true);
        await s.Load();
        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 150_000));

        var statuses = await s.Read(db => db.VehicleKilometersReadings.AsNoTracking()
            .Where(r => r.VehicleId == s.VehicleId).OrderBy(r => r.ReadAt).Select(r => r.Status).ToListAsync());
        Assert.Equal([VehicleKilometersReadingStatus.Rejected, VehicleKilometersReadingStatus.Pending], statuses);
        Assert.Equal(150_000, (await Pending(s)).Kilometers);
    }

    [Fact]
    public async Task A_reviewed_truck_cannot_go_below_and_only_the_office_reviews()
    {
        var s = await new Scenario(fixture).Seed(open: false);
        await s.Load();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Send(new OpenSessionCommand(s.ZoneId, 99)));
        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 100));
        Assert.Equal(0, await s.Read(db => db.VehicleKilometersReadings.CountAsync(r => r.VehicleId == s.VehicleId)));

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new GetPendingKilometersReadingsQuery()));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Send(new ResolveKilometersReadingCommand(Guid.NewGuid(), true)));
    }

    [Fact]
    public async Task The_averages_count_only_from_the_accepted_reading()
    {
        var s = await Unchecked(fixture);
        s.SessionId = await s.Send(new OpenSessionCommand(s.ZoneId, 187_420));
        await s.Send(new CloseSessionCommand(s.SessionId, 187_460, s.Close().Returns), admin: true);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        var query = new GetVehicleKilometersQuery(hoy.AddDays(-1), hoy.AddDays(1));

        // Sin revisar no cuenta: el número no es confiable.
        Assert.DoesNotContain((await s.Send(query, admin: true)).Vehicles, v => v.VehicleId == s.VehicleId);

        await s.Send(new ResolveKilometersReadingCommand((await Pending(s)).Id, Accept: true), admin: true);
        var line = (await s.Send(query, admin: true)).Vehicles.Single(v => v.VehicleId == s.VehicleId);
        Assert.Equal(1, line.Sessions);
        Assert.Equal(40, line.TotalKilometers);
    }
}
