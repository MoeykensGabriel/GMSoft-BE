using FluentValidation;

namespace GMSoft.Application.Features.Products.Create;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Detail)
            .NotEmpty().WithMessage("El detalle es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.CommercialDetail)
            .MaximumLength(200);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");

        RuleFor(x => x.Tracking)
            .IsInEnum().WithMessage("El modo de seguimiento no es válido.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500);
    }
}
