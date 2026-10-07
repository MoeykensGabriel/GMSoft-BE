using FluentValidation;
using GMSoft.Application.Common.Validation;
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

        When(x => x.Type == DeliveryType.ContainerOnly, () =>
        {
            RuleFor(x => x.Items).Empty()
                .WithMessage("Una visita de solo envases no puede incluir productos vendidos.");
        });

        // O se visita a un cliente que ya existe, o se lo da de alta. Las dos cosas
        // a la vez no significa nada, y ninguna deja la visita sin dueño.
        RuleFor(x => x)
            .Must(x => (x.CustomerId is not null) ^ (x.NewCustomer is not null))
            .WithMessage("Hay que indicar un cliente existente o los datos de uno nuevo, no ambos.");

        // Una primera entrega puede ser una venta o una prueba gratuita.
        RuleFor(x => x)
            .Must(x => x.NewCustomer is null ||
                ((x.Type == DeliveryType.Sale || x.Type == DeliveryType.Promotion) && x.Items.Count > 0))
            .WithMessage("Un cliente nuevo se da de alta junto con una venta o promocion.");

        // Una visita de venta sin nada vendido no es una venta.
        RuleFor(x => x)
            .Must(x => (x.Type != DeliveryType.Sale && x.Type != DeliveryType.Promotion) || x.Items.Count > 0)
            .WithMessage("Una venta o promocion necesita al menos un producto.");

        When(x => x.Type == DeliveryType.Promotion, () =>
        {
            RuleFor(x => x.Payment).Null()
                .WithMessage("Una promocion es gratuita y no admite cobros.");
        });

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

            // Sin importe el servidor cobra el total, y solo una venta tiene total.
            RuleFor(x => x)
                .Must(x => x.Payment!.Amount is not null || x.Type == DeliveryType.Sale)
                .WithMessage("Cobrar el total solo aplica a una venta.");
        });

        When(x => x.NewCustomer is not null, () =>
        {
            RuleFor(x => x.NewCustomer!.VisitDays).ValidVisitDays();
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
