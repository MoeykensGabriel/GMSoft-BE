using FluentValidation.TestHelper;
using GMSoft.Application.Features.Products.Create;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Products;

/// <summary>
/// Las validaciones corren solas en el pipeline de MediatR, así que nadie las
/// invoca a mano y un error acá pasa desapercibido hasta que llega mala data.
/// </summary>
public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    private static CreateProductCommand Valido() => new(
        Detail:           "Bidon 20 litros",
        CommercialDetail: "Agua mineral 20L",
        SalePrice:        3500m,
        Tracking:         ContainerTracking.ByBalance,
        IsPublished:      true,
        ImageUrl:         null);

    [Fact]
    public void Un_producto_valido_pasa()
    {
        _validator.TestValidate(Valido()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_detalle_no_puede_estar_vacio(string detalle)
    {
        var command = Valido() with { Detail = detalle };

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.Detail);
    }

    [Fact]
    public void El_precio_no_puede_ser_negativo()
    {
        var command = Valido() with { SalePrice = -1m };

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.SalePrice);
    }

    [Fact]
    public void El_precio_puede_ser_cero()
    {
        // Un envase que se entrega sin cargo sigue siendo un producto del catalogo.
        var command = Valido() with { SalePrice = 0m };

        _validator.TestValidate(command)
            .ShouldNotHaveValidationErrorFor(x => x.SalePrice);
    }

    [Fact]
    public void El_modo_de_seguimiento_tiene_que_ser_uno_de_los_definidos()
    {
        var command = Valido() with { Tracking = (ContainerTracking)99 };

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.Tracking);
    }
}
