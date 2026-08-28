using FluentValidation;

namespace GMSoft.Application.Features.Sessions.Settlement;

public class RegisterSettlementCommandValidator : AbstractValidator<RegisterSettlementCommand>
{
    public RegisterSettlementCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        // Cero es valido: una salida donde no se cobro nada tambien se rinde, y dejar
        // constancia de que llego cero es distinto de no haber rendido.
        RuleFor(x => x.AmountReceived)
            .GreaterThanOrEqualTo(0).WithMessage("El monto recibido no puede ser negativo.");

        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
