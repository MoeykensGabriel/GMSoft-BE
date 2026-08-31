using FluentValidation;

namespace GMSoft.Application.Features.VehicleLoads.Register;

public class RegisterVehicleLoadCommandValidator : AbstractValidator<RegisterVehicleLoadCommand>
{
    public RegisterVehicleLoadCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Hay que cargar al menos un producto.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Cargar cero unidades de un producto no es una carga.");
        });

        // Un producto repetido en la misma tanda suele ser doble tipeo. Sumarlo en
        // silencio deja al camion figurando con stock que no subio.
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Hay productos repetidos en la carga.");
    }
}
