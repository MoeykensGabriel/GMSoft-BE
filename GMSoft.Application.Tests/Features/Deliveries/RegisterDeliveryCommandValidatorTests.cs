using FluentValidation.TestHelper;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Deliveries;

public class RegisterDeliveryCommandValidatorTests
{
    private readonly RegisterDeliveryCommandValidator _validator = new();

    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Bidon   = Guid.NewGuid();
    private static readonly Guid Sifon   = Guid.NewGuid();

    private static RegisterDeliveryCommand Venta() => new(
        CustomerId:    Cliente,
        NewCustomer:   null,
        Type:          DeliveryType.Sale,
        Items:         [new DeliveryItemLine(Bidon, 2)],
        ContainersOut: [new ContainerLine(Bidon, 2)],
        ContainersIn:  [new ContainerLine(Bidon, 2)],
        Payment:       null,
        Notes:         null);

    private static NewCustomerLine ClienteNuevo() =>
        new(BusinessName: null,
            ContactName:  "Juan Perez",
            Phone:        "3811234567",
            Address:      "Av Siempreviva 742",
            Notes:        null);

    [Fact]
    public void Una_venta_normal_pasa()
    {
        _validator.TestValidate(Venta()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void O_cliente_existente_o_cliente_nuevo_pero_no_los_dos()
    {
        var command = Venta() with { NewCustomer = ClienteNuevo() };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void La_visita_no_puede_quedar_sin_cliente()
    {
        var command = Venta() with { CustomerId = null };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void El_chofer_da_de_alta_un_cliente_solo_si_le_vende_algo()
    {
        // Cliente nuevo en una visita que no vende nada: es la regla del negocio.
        var command = Venta() with
        {
            CustomerId  = null,
            NewCustomer = ClienteNuevo(),
            Type        = DeliveryType.ContainerOnly,
            Items       = []
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Un_cliente_nuevo_con_venta_pasa()
    {
        var command = Venta() with { CustomerId = null, NewCustomer = ClienteNuevo() };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Una_venta_sin_productos_no_es_una_venta()
    {
        var command = Venta() with { Items = [] };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Una_visita_de_solo_envases_no_necesita_productos()
    {
        var command = Venta() with
        {
            Type          = DeliveryType.ContainerOnly,
            Items         = [],
            ContainersOut = [],
            ContainersIn  = [new ContainerLine(Bidon, 3)]
        };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Una_visita_de_solo_envases_que_no_mueve_envases_no_es_nada()
    {
        var command = Venta() with
        {
            Type          = DeliveryType.ContainerOnly,
            Items         = [],
            ContainersOut = [],
            ContainersIn  = []
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void No_se_puede_repetir_un_producto_en_los_envases_devueltos()
    {
        // Distintas cantidades: como objetos son lineas distintas, pero duplicarian
        // el movimiento de envases del cliente.
        var command = Venta() with
        {
            ContainersIn = [new ContainerLine(Bidon, 2), new ContainerLine(Bidon, 3)]
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ContainersIn);
    }

    [Fact]
    public void Envases_de_dos_productos_distintos_conviven()
    {
        var command = Venta() with
        {
            ContainersIn = [new ContainerLine(Bidon, 2), new ContainerLine(Sifon, 1)]
        };

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.ContainersIn);
    }

    [Fact]
    public void Un_cobro_de_cero_no_es_un_cobro()
    {
        var command = Venta() with { Payment = new PaymentLine(0m, PaymentMethod.Cash) };

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor("Payment.Amount");
    }
}
