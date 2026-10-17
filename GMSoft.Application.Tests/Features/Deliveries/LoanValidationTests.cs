using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Products.Create;
using GMSoft.Application.Features.Products.Update;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Deliveries;

public class LoanValidationTests
{
    private static RegisterDeliveryCommand Visit() => new(Guid.NewGuid(), null, DeliveryType.ContainerOnly,
        [], [], [], null, null, Guid.NewGuid());

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(true, 1001)]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(false, 1001)]
    public void Cantidades_invalidas(bool outgoing, int quantity)
    {
        var lines = new ContainerLine[] { new(Guid.NewGuid(), quantity) };
        var request = outgoing ? Visit() with { LoansOut = lines } : Visit() with { LoansIn = lines };
        Assert.False(new RegisterDeliveryCommandValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Solo_prestamos_valido_pero_repetidos_sin_producto_y_no_compro_no(bool outgoing)
    {
        var line = new ContainerLine(Guid.NewGuid(), 1);
        var request = outgoing ? Visit() with { LoansOut = [line] } : Visit() with { LoansIn = [line] };
        var validator = new RegisterDeliveryCommandValidator();
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(outgoing ? request with { LoansOut = [line, line] } : request with { LoansIn = [line, line] }).IsValid);
        Assert.False(validator.Validate(outgoing ? request with { LoansOut = [line with { ProductId = Guid.Empty }] }
            : request with { LoansIn = [line with { ProductId = Guid.Empty }] }).IsValid);
        Assert.False(validator.Validate(request with { Type = DeliveryType.NoPurchase, NoPurchaseReason = NoPurchaseReason.Other, Notes = "No estaba" }).IsValid);
        Assert.False(validator.Validate(outgoing ? Visit() with { LoansOut = null! } : Visit() with { LoansIn = null! }).IsValid);
    }

    [Theory]
    [InlineData(ContainerTracking.None, true)]
    [InlineData(ContainerTracking.ByBalance, false)]
    [InlineData(ContainerTracking.ByUnit, false)]
    public void Solo_sin_envase_puede_prestarse(ContainerTracking tracking, bool valid)
    {
        Assert.Equal(valid, new CreateProductCommandValidator().Validate(new CreateProductCommand("Dispenser", null, 5000, tracking, true, null, true)).IsValid);
        Assert.Equal(valid, new UpdateProductCommandValidator().Validate(new UpdateProductCommand(Guid.NewGuid(), "Dispenser", null, 5000, tracking, true, null, true)).IsValid);
    }
}
