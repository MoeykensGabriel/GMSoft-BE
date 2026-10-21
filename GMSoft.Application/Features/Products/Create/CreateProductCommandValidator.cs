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

        RuleFor(x => x.ExchangeGroup)
            .Must(group => group == null || group.Trim().Length <= 50)
            .WithMessage("El grupo de intercambio no puede superar los 50 caracteres.")
            .Must((x, group) => string.IsNullOrWhiteSpace(group) || x.Tracking == GMSoft.Domain.Enums.ContainerTracking.ByBalance)
            .WithMessage("Solo los productos con envase por saldo pueden tener grupo de intercambio.");

        RuleFor(x => x.CanBeLent)
            .Must((x, lent) => !lent || x.Tracking == GMSoft.Domain.Enums.ContainerTracking.None)
            .WithMessage("Solo se pueden prestar productos sin envase.");

        RuleFor(x => x.Tracking)
            .IsInEnum().WithMessage("El modo de seguimiento no es válido.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500);
    }
}
