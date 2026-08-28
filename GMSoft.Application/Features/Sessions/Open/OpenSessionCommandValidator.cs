using FluentValidation;

namespace GMSoft.Application.Features.Sessions.Open;

public class OpenSessionCommandValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionCommandValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("Hay que elegir la zona de reparto.");

        RuleFor(x => x.KilometersAtOpen)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");

        RuleFor(x => x.Load).NotNull();

        RuleForEach(x => x.Load).ChildRules(linea =>
        {
            linea.RuleFor(l => l.ProductId).NotEmpty();
            linea.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Cargar cero unidades de un producto no es una carga.");
        });

        // Un producto repetido en la carga suele ser doble tipeo, y sumarlo en
        // silencio arranca la sesion con stock que no subio al camion.
        RuleFor(x => x.Load)
            .Must(load => load.Select(l => l.ProductId).Distinct().Count() == load.Count)
            .WithMessage("Hay productos repetidos en la carga.");
    }
}
