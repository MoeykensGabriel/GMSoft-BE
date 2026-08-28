using FluentValidation.TestHelper;
using GMSoft.Application.Features.Sessions.Open;

namespace GMSoft.Application.Tests.Features.Sessions;

public class OpenSessionCommandValidatorTests
{
    private readonly OpenSessionCommandValidator _validator = new();

    private static readonly Guid Bidon  = Guid.NewGuid();
    private static readonly Guid Sifon  = Guid.NewGuid();
    private static readonly Guid Palermo = Guid.NewGuid();

    private static OpenSessionCommand Valido(params OpenSessionLoadLine[] carga) =>
        new(ZoneId: Palermo,
            KilometersAtOpen: 120_000,
            Load: carga.Length > 0 ? carga : [new OpenSessionLoadLine(Bidon, 100)]);

    [Fact]
    public void Una_apertura_valida_pasa()
    {
        _validator.TestValidate(Valido()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Se_puede_salir_sin_carga()
    {
        // Un chofer que sale solo a levantar envases no carga nada.
        var command = Valido() with { Load = [] };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Hay_que_elegir_zona()
    {
        var command = Valido() with { ZoneId = Guid.Empty };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ZoneId);
    }

    [Fact]
    public void No_se_puede_repetir_un_producto_en_la_carga()
    {
        // Sumarlo en silencio arrancaria la sesion con stock que no subio al camion,
        // y el faltante del cierre daria mal sin que nadie sepa por que.
        var command = Valido(
            new OpenSessionLoadLine(Bidon, 100),
            new OpenSessionLoadLine(Bidon, 50));

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Load);
    }

    [Fact]
    public void Dos_productos_distintos_conviven()
    {
        var command = Valido(
            new OpenSessionLoadLine(Bidon, 100),
            new OpenSessionLoadLine(Sifon, 40));

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Cargar_cero_unidades_no_es_una_carga()
    {
        var command = Valido(new OpenSessionLoadLine(Bidon, 0));

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor("Load[0].Quantity");
    }
}
