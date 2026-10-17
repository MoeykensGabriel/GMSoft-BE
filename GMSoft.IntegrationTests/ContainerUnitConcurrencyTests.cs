using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.ContainerUnits.Assign;
using GMSoft.Application.Features.ContainerUnits.Decommission;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// Un dispenser identificado está en un solo lugar. Dos operaciones de la oficina sobre la
/// misma unidad al mismo tiempo: gana una y la otra recibe un conflicto.
/// </summary>
[Collection("PostgreSQL")]
public sealed class ContainerUnitConcurrencyTests(PostgresFixture fixture)
{
    private async Task<(Scenario f, Guid unit, Customer a, Customer b)> Unidad()
    {
        var f = await new Scenario(fixture).Seed();
        var unit = new ContainerUnit { Id = Guid.NewGuid(), ProductId = f.UnitId, SerialNumber = "DSP-" + Guid.NewGuid().ToString("N")[..8] };
        Customer a = f.Customer(8), b = f.Customer(9);
        await f.Write(db => { db.ContainerUnits.Add(unit); db.Customers.AddRange(a, b); return Task.CompletedTask; });
        return (f, unit.Id, a, b);
    }

    private static Task<List<ContainerMovement>> Movimientos(Scenario f, Guid unit) =>
        f.Read(db => db.ContainerMovements.Where(m => m.ContainerUnitId == unit).ToListAsync());

    [Fact, Trait("Grupo", "C")]
    public async Task Dos_entregas_simultaneas_del_mismo_dispenser_entregan_una_sola()
    {
        var (f, unit, a, b) = await Unidad();
        // Las dos leen la unidad en depósito antes de que cualquiera la entregue.
        var barrier = new SqlReadBarrier("ContainerUnits", "LIMIT");
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => f.Send(new AssignContainerUnitCommand(unit, a.Id, null), true, barrier)),
            Record.ExceptionAsync(() => f.Send(new AssignContainerUnitCommand(unit, b.Id, null), true, barrier)));
        Assert.Single(errors, e => e is null);
        Assert.IsType<ConflictException>(Assert.Single(errors, e => e is not null));
        var entregas = (await Movimientos(f, unit)).Where(m => m.Type == ContainerMovementType.DeliveredToCustomer).ToList();
        var final = await f.Read(db => db.ContainerUnits.SingleAsync(u => u.Id == unit));
        Assert.Equal(final.CurrentCustomerId, Assert.Single(entregas).CustomerId);
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Entrega_y_baja_simultaneas_no_dejan_un_dispenser_dado_de_baja_en_poder_de_un_cliente()
    {
        var (f, unit, a, _) = await Unidad();
        var barrier = new SqlReadBarrier("ContainerUnits", "LIMIT");
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => f.Send(new AssignContainerUnitCommand(unit, a.Id, null), true, barrier)),
            Record.ExceptionAsync(() => f.Send(new DecommissionContainerUnitCommand(unit, "Roto"), true, barrier)));
        var final = await f.Read(db => db.ContainerUnits.SingleAsync(u => u.Id == unit));
        // Si quedó fuera de servicio, no puede figurar en un cliente; y lo que dice el libro
        // de movimientos tiene que cuadrar con dónde está.
        Assert.False(final.Status == ContainerUnitStatus.OutOfService && final.CurrentCustomerId is not null);
        var neto = (await Movimientos(f, unit)).Sum(m => m.Quantity);
        Assert.Equal(final.CurrentCustomerId is null ? 0 : 1, neto);
        Assert.All(errors.Where(e => e is not null), e => Assert.IsType<ConflictException>(e));
    }
}
