using FluentValidation.TestHelper;
using GMSoft.Application.Features.Sessions.Open;

namespace GMSoft.Application.Tests.Features.Sessions;

public class OpenSessionCommandValidatorTests
{
    private readonly OpenSessionCommandValidator _validator = new();

    private static readonly Guid Palermo = Guid.NewGuid();

    private static OpenSessionCommand Valido() =>
        new(ZoneId: Palermo, KilometersAtOpen: 120_000);

    [Fact]
    public void Una_apertura_valida_pasa()
    {
        _validator.TestValidate(Valido()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Hay_que_elegir_zona()
    {
        var command = Valido() with { ZoneId = Guid.Empty };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ZoneId);
    }

    [Fact]
    public void El_kilometraje_no_puede_ser_negativo()
    {
        var command = Valido() with { KilometersAtOpen = -1 };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.KilometersAtOpen);
    }

    [Fact]
    public void La_apertura_no_declara_carga()
    {
        // La carga la subio la oficina antes de que el chofer llegara: la apertura
        // se lleva lo que el camion tenga arriba. Que el chofer la declarara volvia
        // el control de recepcion una copia de lo que el mismo habia dicho.
        Assert.DoesNotContain(
            typeof(OpenSessionCommand).GetProperties(),
            p => p.Name == "Load");
    }
}
