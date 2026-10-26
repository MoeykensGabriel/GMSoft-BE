using System.Reflection;
using System.Text.Json;
using GMSoft.API.Controllers;
using GMSoft.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Vehicles.Correction;
using GMSoft.Application.Features.Vehicles.Update;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class VehicleKilometersCorrectionTests(PostgresFixture fixture)
{
    private static UpdateVehicleCommand Update(Scenario f, int km, string? reason = null)
        => new(f.VehicleId, "Camión", f.VehicleId.ToString("N")[..12], VehicleType.Truck, km, reason);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bajar_sin_motivo_es_bad_request(string? reason)
    {
        var f = await new Scenario(fixture).Seed(open: false);
        var error = await Assert.ThrowsAsync<BadRequestException>(() => f.Send(Update(f, 50, reason), true));
        await AssertHttpError(error, 400);
        Assert.Equal("Para bajar el kilometraje escribí el motivo.", error.Message);
        Assert.Equal(100, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        Assert.Empty(await f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId), true));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(51)]
    public async Task Corregir_guarda_usuario_y_nueva_base_sin_tocar_salidas_viejas(int opening)
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Close(), true);
        await f.Write(async db => (await db.Vehicles.FindAsync(f.VehicleId))!.CurrentKilometers = 300000100);
        var before = DateTime.UtcNow;
        await f.Send(Update(f, 50, " Lectura real del odómetro "), true);
        Assert.Equal(50, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        var correction = await f.Read(db => db.VehicleKilometersCorrections.SingleAsync(c => c.VehicleId == f.VehicleId));
        Assert.Equal((300000100, 50), (correction.PreviousKilometers, correction.NewKilometers));
        Assert.Equal(f.AdminId, correction.CorrectedByUserId);
        Assert.Equal("Lectura real del odómetro", correction.Reason);
        Assert.InRange(correction.CorrectedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, correction.CorrectedAt.Kind);
        var old = await f.Read(db => db.DeliverySessions.SingleAsync(s => s.Id == f.SessionId));
        Assert.Equal(100, old.KilometersAtOpen);
        Assert.Equal(110, old.KilometersAtClose);
        var history = Assert.Single(await f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId), true));
        Assert.Equal(f.AdminId.ToString("N"), history.CorrectedByName);
        Assert.Equal(correction.CorrectedAt, history.CorrectedAt);
        Assert.Equal((300000100, 50, correction.Reason), (history.PreviousKilometers, history.NewKilometers, history.Reason));
        await f.Load();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Send(new OpenSessionCommand(f.ZoneId, 49)));
        var session = await f.Send(new OpenSessionCommand(f.ZoneId, opening));
        Assert.NotEqual(Guid.Empty, session);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    public async Task Subir_o_mantener_sin_motivo_no_registra(int km)
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await f.Send(Update(f, km), true);
        Assert.Equal(km, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        Assert.Empty(await f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId), true));
    }

    [Fact]
    public async Task Bajar_con_salida_abierta_es_conflict_y_no_modifica_nada()
    {
        var f = await new Scenario(fixture).Seed();
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Send(Update(f, 50, "Error de carga"), true));
        await AssertHttpError(error, 409);
        Assert.Equal("El camión está en la calle: corregí el kilometraje cuando lo recibas.", error.Message);
        Assert.Equal(100, await f.Read(db => db.Vehicles.Where(v => v.Id == f.VehicleId).Select(v => v.CurrentKilometers).SingleAsync()));
        Assert.Empty(await f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId), true));
    }

    [Fact]
    public async Task Historial_ordenado_y_solo_admin_con_validaciones()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(Update(f, 50, "Error")));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId)));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(Update(f, 50, new string('x', 301)), true));
        await Assert.ThrowsAsync<ValidationException>(() => f.Send(Update(f, -1, "Error"), true));
        await f.Send(Update(f, 80, "Primera"), true);
        await f.Write(async db => (await db.VehicleKilometersCorrections.SingleAsync(c => c.VehicleId == f.VehicleId)).CorrectedAt = DateTime.UtcNow.AddDays(-1));
        await f.Send(Update(f, 50, "Segunda"), true);
        var history = await f.Send(new GetVehicleKilometersCorrectionsQuery(f.VehicleId), true);
        Assert.Equal(new[] { "Segunda", "Primera" }, history.Select(c => c.Reason));
    }
    private static async Task AssertHttpError(Exception error, int status)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, error, default);
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(error.Message, json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public void Historial_y_actualizacion_son_solo_admin()
    {
        var endpoint = typeof(VehiclesController).GetMethod(nameof(VehiclesController.GetKilometersCorrections))!;
        Assert.Equal("Admin", endpoint.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("{id:guid}/kilometers-corrections", endpoint.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("Admin", typeof(VehiclesController).GetMethod(nameof(VehiclesController.Update))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

}
