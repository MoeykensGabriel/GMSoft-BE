using FluentValidation;

namespace GMSoft.Application.Features.Containers.Adjust;

public class AdjustCustomerContainersCommandValidator
    : AbstractValidator<AdjustCustomerContainersCommand>
{
    public AdjustCustomerContainersCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.RealQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Un cliente no puede tener menos de cero envases.");

        // Un ajuste sin motivo deja el saldo cambiado y sin explicacion, que es
        // exactamente lo que el libro mayor existe para evitar.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Hay que decir por que se corrige el saldo.")
            .MaximumLength(500);
    }
}
