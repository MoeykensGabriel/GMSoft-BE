using FluentValidation;

namespace GMSoft.Application.Features.Sessions.Open;

public class OpenSessionCommandValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionCommandValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("Hay que elegir la zona de reparto.");

        RuleFor(x => x.KilometersAtOpen)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");
    }
}
