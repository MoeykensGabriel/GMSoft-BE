using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.RouteOrder;
using GMSoft.Application.Features.Sessions.Unvisited;
using GMSoft.Application.Features.Vehicles.Schedule;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// Una salida que cruza zonas el mismo día (San Miguel y después Tafí Viejo): el chofer
/// atiende a los clientes de todas, en el orden en que las eligió, y cada cliente
/// conserva su zona real.
/// </summary>
[Collection("PostgreSQL")]
public class MultiZoneSessionTests(PostgresFixture fixture)
{
    private sealed record Zonas(Scenario F, Guid Segunda, Guid Ajena, Guid ClienteSegunda, Guid ClienteAjeno);

    private async Task<Zonas> Seed(bool open = true)
    {
        var f = await new Scenario(fixture).Seed(open: false);
        var segunda = Guid.NewGuid();
        var ajena = Guid.NewGuid();
        var enSegunda = f.Customer(order: 1); enSegunda.ZoneId = segunda;
        var enAjena = f.Customer(order: 1); enAjena.ZoneId = ajena;
        await f.Write(db =>
        {
            db.Zones.AddRange(new Zone { Id = segunda, Name = "Segunda " + segunda }, new Zone { Id = ajena, Name = "Ajena " + ajena });
            db.Customers.AddRange(enSegunda, enAjena);
            return Task.CompletedTask;
        });
        if (open)
        {
            await f.Load();
            f.SessionId = await f.Send(new OpenSessionCommand(Guid.Empty, 100, ZoneIds: [f.ZoneId, segunda]));
        }
        return new(f, segunda, ajena, enSegunda.Id, enAjena.Id);
    }

    [Fact]
    public async Task Abre_con_las_zonas_en_orden_y_la_primera_queda_como_principal()
    {
        var z = await Seed();

        var zonas = await z.F.Read(db => db.DeliverySessionZones.Where(x => x.DeliverySessionId == z.F.SessionId)
            .OrderBy(x => x.Position).Select(x => x.ZoneId).ToListAsync());
        Assert.Equal([z.F.ZoneId, z.Segunda], zonas);
        var actual = await z.F.Send(new GetCurrentSessionQuery());
        Assert.Equal(z.F.ZoneId, actual!.ZoneId);
        Assert.Equal([z.F.ZoneId, z.Segunda], actual.Zones.Select(x => x.ZoneId));
    }

    [Fact]
    public async Task Vende_en_cualquiera_de_sus_zonas_y_no_en_una_que_no_recorre()
    {
        var z = await Seed();

        await z.F.Send(z.F.Sale());
        await z.F.Send(z.F.Sale() with { CustomerId = z.ClienteSegunda });
        Assert.Equal(2, await z.F.Sales());

        await Assert.ThrowsAsync<BadRequestException>(() => z.F.Send(z.F.Sale() with { CustomerId = z.ClienteAjeno }));
    }

    [Fact]
    public async Task Sin_visitar_trae_las_zonas_en_el_orden_de_la_salida()
    {
        var z = await Seed();

        var pendientes = await z.F.Send(new GetUnvisitedCustomersQuery(z.F.SessionId));

        // El cliente de la principal tiene RouteOrder 7 y el de la segunda 1: manda la zona, no el número.
        Assert.Equal([z.F.CustomerId, z.ClienteSegunda], pendientes.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task El_chofer_lista_cada_zona_de_su_salida_y_una_ajena_cae_en_la_principal()
    {
        var z = await Seed();

        var segunda = await z.F.Send(new GetCustomersQuery(PageSize: 50, ZoneId: z.Segunda));
        Assert.Equal([z.ClienteSegunda], segunda.Items.Select(c => c.Id));
        var ajena = await z.F.Send(new GetCustomersQuery(PageSize: 50, ZoneId: z.Ajena));
        Assert.Equal([z.F.CustomerId], ajena.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task El_cliente_nuevo_queda_en_la_zona_elegida_y_no_en_una_ajena()
    {
        var z = await Seed();
        RegisterDeliveryCommand Nuevo(Guid? zona, string nombre) => z.F.Sale() with
        {
            CustomerId = null,
            NewCustomer = new NewCustomerLine(null, nombre, "381", "Calle 9", null, [1], zona)
        };

        var creado = await z.F.Send(Nuevo(z.Segunda, "En la segunda"));
        Assert.Equal(z.Segunda, await z.F.Read(db => db.Customers.Where(c => c.Id == creado.CustomerId).Select(c => c.ZoneId).SingleAsync()));
        var principal = await z.F.Send(Nuevo(null, "Sin elegir"));
        Assert.Equal(z.F.ZoneId, await z.F.Read(db => db.Customers.Where(c => c.Id == principal.CustomerId).Select(c => c.ZoneId).SingleAsync()));

        await Assert.ThrowsAsync<BadRequestException>(() => z.F.Send(Nuevo(z.Ajena, "En la ajena")));
    }

    [Fact]
    public async Task Organizar_recorrido_es_por_zona_y_solo_entre_las_de_la_salida()
    {
        var z = await Seed();

        var principal = await z.F.Send(new GetDriverRouteOrderQuery(z.F.SessionId));
        Assert.Equal([z.F.CustomerId], principal.Items.Select(c => c.Id));
        var segunda = await z.F.Send(new GetDriverRouteOrderQuery(z.F.SessionId, z.Segunda));
        Assert.Equal([z.ClienteSegunda], segunda.Items.Select(c => c.Id));

        // Mezclar clientes de dos zonas no es un orden válido de ninguna.
        await Assert.ThrowsAsync<ConflictException>(() => z.F.Send(new SaveDriverRouteOrderCommand(z.F.SessionId,
            [z.ClienteSegunda, z.F.CustomerId], segunda.Version, z.Segunda)));
        var guardado = await z.F.Send(new SaveDriverRouteOrderCommand(z.F.SessionId, [z.ClienteSegunda], segunda.Version, z.Segunda));
        Assert.Equal(segunda.Version, guardado.Version);

        await Assert.ThrowsAsync<BadRequestException>(() => z.F.Send(new GetDriverRouteOrderQuery(z.F.SessionId, z.Ajena)));
    }

    [Fact]
    public async Task Reordenar_la_segunda_zona_queda_a_la_vista_en_el_detalle_de_la_salida()
    {
        var z = await Seed();
        var otro = z.F.Customer(order: 2); otro.ZoneId = z.Segunda;
        await z.F.Write(db => { db.Customers.Add(otro); return Task.CompletedTask; });
        Assert.Null((await z.F.Send(new GetSessionByIdQuery(z.F.SessionId), true)).DriverRouteOrderChange);

        var segunda = await z.F.Send(new GetDriverRouteOrderQuery(z.F.SessionId, z.Segunda));
        await z.F.Send(new SaveDriverRouteOrderCommand(z.F.SessionId, [otro.Id, z.ClienteSegunda], segunda.Version, z.Segunda));

        var cambio = (await z.F.Send(new GetSessionByIdQuery(z.F.SessionId), true)).DriverRouteOrderChange;
        Assert.NotNull(cambio);
        Assert.Equal(2, cambio.MovedCount);
    }

    [Fact]
    public async Task El_listado_de_salidas_la_encuentra_por_cualquiera_de_sus_zonas()
    {
        var z = await Seed();

        var porSegunda = await z.F.Send(new GetSessionsQuery(ZoneId: z.Segunda), true);
        Assert.Contains(porSegunda.Items, s => s.Id == z.F.SessionId);
        var porAjena = await z.F.Send(new GetSessionsQuery(ZoneId: z.Ajena), true);
        Assert.DoesNotContain(porAjena.Items, s => s.Id == z.F.SessionId);
    }

    [Fact]
    public async Task Abrir_rechaza_zonas_repetidas_vacias_o_inexistentes_y_acepta_la_zona_sola_de_antes()
    {
        var z = await Seed(open: false);
        await z.F.Load();

        await Assert.ThrowsAsync<ValidationException>(() => z.F.Send(new OpenSessionCommand(Guid.Empty, 100, ZoneIds: [z.F.ZoneId, z.F.ZoneId])));
        await Assert.ThrowsAsync<ValidationException>(() => z.F.Send(new OpenSessionCommand(Guid.Empty, 100)));
        await Assert.ThrowsAsync<NotFoundException>(() => z.F.Send(new OpenSessionCommand(Guid.Empty, 100, ZoneIds: [z.F.ZoneId, Guid.NewGuid()])));

        // La app vieja manda solo ZoneId: es una salida de una zona.
        z.F.SessionId = await z.F.Send(new OpenSessionCommand(z.F.ZoneId, 100));
        Assert.Equal([z.F.ZoneId], (await z.F.Send(new GetCurrentSessionQuery()))!.Zones.Select(x => x.ZoneId));
    }

    [Fact]
    public async Task La_agenda_guarda_varias_zonas_por_dia_las_sugiere_y_cubre_a_los_clientes_de_todas()
    {
        var z = await Seed(open: false);
        ScheduleDayInput[] Semana(params ScheduleDayInput[] dias) => Enumerable.Range(1, 7)
            .Select(d => dias.FirstOrDefault(x => x.Day == d) ?? new ScheduleDayInput(d, null)).ToArray();

        var guardada = await z.F.Send(new SaveVehicleScheduleCommand(z.F.VehicleId,
            Semana(new ScheduleDayInput(1, null, [z.F.ZoneId, z.Segunda]), new ScheduleDayInput(2, z.Ajena))), true);

        var lunes = guardada.Days.Single(d => d.Day == 1);
        Assert.Equal([z.F.ZoneId, z.Segunda], lunes.Zones.Select(x => x.ZoneId));
        Assert.Equal(z.F.ZoneId, lunes.ZoneId);
        Assert.Equal([z.Ajena], guardada.Days.Single(d => d.Day == 2).Zones.Select(x => x.ZoneId));
        Assert.Empty(guardada.Days.Single(d => d.Day == 3).Zones);

        // El cliente de la segunda zona del lunes está cubierto.
        Assert.True((await z.F.Send(new CheckCoverageQuery(z.F.VehicleId, z.Segunda, [1]), true)).Covered);
        Assert.False((await z.F.Send(new CheckCoverageQuery(z.F.VehicleId, z.Ajena, [1]), true)).Covered);

        // La carga es de lunes: se sugiere abrir con las dos zonas, en orden.
        await z.F.Load();
        var sugerida = await z.F.Send(new SuggestedZoneQuery());
        Assert.Equal([z.F.ZoneId, z.Segunda], sugerida.Zones.Select(x => x.ZoneId));
        Assert.Equal(z.F.ZoneId, sugerida.ZoneId);

        await Assert.ThrowsAsync<ValidationException>(() => z.F.Send(new SaveVehicleScheduleCommand(z.F.VehicleId,
            Semana(new ScheduleDayInput(1, null, [z.Segunda, z.Segunda]))), true));
    }
}
