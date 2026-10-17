using GMSoft.Application.Features.Containers.Adjust;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

/// <summary>
/// El saldo de envases de un cliente es la suma de sus movimientos. Lo pueden tocar a la
/// vez el chofer (una venta con devolución) y la oficina (un ajuste por conteo).
/// </summary>
[Collection("PostgreSQL")]
public sealed class ContainerBalanceConcurrencyTests(PostgresFixture fixture)
{
    private static Task<int> Saldo(Scenario f) => f.Read(db => db.CustomerContainerBalances
        .Where(b => b.CustomerId == f.CustomerId && b.ProductId == f.ProductId).Select(b => b.Quantity).SingleAsync());

    private static Task<int> SumaDeMovimientos(Scenario f) => f.Read(db => db.ContainerMovements
        .Where(m => m.CustomerId == f.CustomerId && m.ProductId == f.ProductId).SumAsync(m => m.Quantity));

    [Fact, Trait("Grupo", "C")]
    public async Task Venta_con_devolucion_y_ajuste_simultaneos_no_pierden_movimientos()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(3));
        // Las dos operaciones leen el saldo (3) antes de que cualquiera escriba.
        var barrier = new SqlReadBarrier("CustomerContainerBalances", "\"CustomerId\" =");
        await Task.WhenAll(
            f.Send(f.Sale(1, returned: 3), barrier: barrier),
            f.Send(new AdjustCustomerContainersCommand(f.CustomerId, f.ProductId, 10, "Conteo en el domicilio"), true, barrier));
        Assert.Equal(await SumaDeMovimientos(f), await Saldo(f));
    }

    [Fact, Trait("Grupo", "C")]
    public async Task Dos_ajustes_simultaneos_dejan_el_saldo_igual_a_la_suma_de_movimientos()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(3));
        // Se encuentran antes de leer: con el bloqueo, el segundo espera al primero.
        var barrier = new SqlReadBarrier("CustomerContainerBalances", "\"CustomerId\" =", beforeExecution: true);
        await Task.WhenAll(
            f.Send(new AdjustCustomerContainersCommand(f.CustomerId, f.ProductId, 5, "Conteo A"), true, barrier),
            f.Send(new AdjustCustomerContainersCommand(f.CustomerId, f.ProductId, 8, "Conteo B"), true, barrier));
        var saldo = await Saldo(f);
        Assert.Equal(await SumaDeMovimientos(f), saldo);
        // Cada ajuste fija una cantidad real: el último en aplicarse manda.
        Assert.Contains(saldo, new[] { 5, 8 });
    }
}
