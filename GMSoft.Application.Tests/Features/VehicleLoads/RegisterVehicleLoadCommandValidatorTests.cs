using FluentValidation.TestHelper;
using GMSoft.Application.Features.VehicleLoads.Register;

namespace GMSoft.Application.Tests.Features.VehicleLoads;

/// <summary>
/// Las reglas de la carga del camión. Vivían en la apertura de la sesión, cuando la
/// declaraba el chofer; se mudaron acá junto con la carga, que ahora la sube la
/// oficina antes de que el camión salga.
/// </summary>
public class RegisterVehicleLoadCommandValidatorTests
{
    private readonly RegisterVehicleLoadCommandValidator _validator = new();

    private static readonly Guid Camion = Guid.NewGuid();
    private static readonly Guid Bidon  = Guid.NewGuid();
    private static readonly Guid Sifon  = Guid.NewGuid();

    private static RegisterVehicleLoadCommand Valido(params VehicleLoadItem[] items) =>
        new(VehicleId: Camion,
            Items: items.Length > 0 ? items : [new VehicleLoadItem(Bidon, 100)]);

    [Fact]
    public void Una_carga_valida_pasa()
    {
        _validator.TestValidate(Valido()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Hay_que_decir_a_que_vehiculo()
    {
        var command = Valido() with { VehicleId = Guid.Empty };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.VehicleId);
    }

    [Fact]
    public void Una_carga_vacia_no_es_una_carga()
    {
        // Distinto de la apertura: ahi salir sin carga es valido (se va solo a
        // levantar envases). Aca alguien apreto "cargar" sin poner nada.
        var command = Valido() with { Items = [] };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void No_se_puede_repetir_un_producto_en_la_carga()
    {
        // Sumarlo en silencio dejaria al camion con stock que no subio, y el faltante
        // del cierre daria mal sin que nadie sepa por que.
        var command = Valido(
            new VehicleLoadItem(Bidon, 100),
            new VehicleLoadItem(Bidon, 50));

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Dos_productos_distintos_conviven()
    {
        var command = Valido(
            new VehicleLoadItem(Bidon, 100),
            new VehicleLoadItem(Sifon, 40));

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Cargar_cero_unidades_no_es_una_carga()
    {
        var command = Valido(new VehicleLoadItem(Bidon, 0));

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor("Items[0].Quantity");
    }
}
