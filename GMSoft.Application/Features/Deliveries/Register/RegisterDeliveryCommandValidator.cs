using FluentValidation;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Deliveries.Register;

public class RegisterDeliveryCommandValidator : AbstractValidator<RegisterDeliveryCommand>
{
    public RegisterDeliveryCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.ContainersOut).NotNull();
        RuleFor(x => x.ContainersIn).NotNull();
        RuleFor(x => x.Notes).MaximumLength(1000);

        // O se visita a un cliente que ya existe, o se lo da de alta. Las dos cosas
        // a la vez no significa nada, y ninguna deja la visita sin dueño.
        RuleFor(x => x)
            .Must(x => (x.CustomerId is not null) ^ (x.NewCustomer is not null))
            .WithMessage("Hay que indicar un cliente existente o los datos de uno nuevo, no ambos.");

        // La regla del negocio: el chofer da de alta un cliente solo si le vende algo.
        RuleFor(x => x)
            .Must(x => x.NewCustomer is null || (x.Type == DeliveryType.Sale && x.Items.Count > 0))
            .WithMessage("Un cliente nuevo se da de alta solo junto con una venta.");

        // Una visita de venta sin nada vendido no es una venta.
        RuleFor(x => x)
            .Must(x => x.Type != DeliveryType.Sale || x.Items.Count > 0)
            .WithMessage("Una visita de venta necesita al menos un producto.");

        // Y una visita que no vende ni mueve envases no paso nada.
        RuleFor(x => x)
            .Must(x => x.Type != DeliveryType.ContainerOnly ||
                       x.ContainersOut.Count > 0 || x.ContainersIn.Count > 0)
            .WithMessage("Una visita sin venta tiene que mover envases.");

        RuleForEach(x => x.Items).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        RuleForEach(x => x.ContainersOut).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        RuleForEach(x => x.ContainersIn).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        // Repetir un producto duplicaria el movimiento de envases del cliente en
        // silencio, que es el error mas caro del sistema.
        RuleFor(x => x.Items)
            .Must(l => SinProductosRepetidos(l.Select(i => i.ProductId)))
            .WithMessage("Hay productos repetidos en la venta.");
        RuleFor(x => x.ContainersOut)
            .Must(l => SinProductosRepetidos(l.Select(i => i.ProductId)))
            .WithMessage("Hay productos repetidos en los envases entregados.");
        RuleFor(x => x.ContainersIn)
            .Must(l => SinProductosRepetidos(l.Select(i => i.ProductId)))
            .WithMessage("Hay productos repetidos en los envases devueltos.");

        When(x => x.Payment is not null, () =>
        {
            RuleFor(x => x.Payment!.Amount)
                .GreaterThan(0).WithMessage("Un cobro de cero no es un cobro.");
            RuleFor(x => x.Payment!.Method).IsInEnum();
        });

        When(x => x.NewCustomer is not null, () =>
        {
            RuleFor(x => x.NewCustomer!.ContactName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.NewCustomer!.Phone).NotEmpty().MaximumLength(30);
            RuleFor(x => x.NewCustomer!.Address).NotEmpty().MaximumLength(300);
            RuleFor(x => x.NewCustomer!.BusinessName).MaximumLength(200);
            RuleFor(x => x.NewCustomer!.Notes).MaximumLength(1000);
        });
    }

    /// <summary>
    /// Se compara por producto y no por linea entera: dos lineas del mismo producto
    /// con cantidades distintas son distintas como objetos, pero son exactamente el
    /// caso que hay que atajar. Ademas el indice unico de ContainerMovement es por
    /// (visita, producto, tipo), asi que una repetida reventaria al guardar.
    /// </summary>
    private static bool SinProductosRepetidos(IEnumerable<Guid> productIds)
    {
        var lista = productIds.ToList();
        return lista.Count == lista.Distinct().Count();
    }
}
