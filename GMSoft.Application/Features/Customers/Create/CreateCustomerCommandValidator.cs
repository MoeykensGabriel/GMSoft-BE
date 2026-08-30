using FluentValidation;

namespace GMSoft.Application.Features.Customers.Create;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.BusinessName).MaximumLength(200);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        // El tope tiene que estar aunque el email sea opcional: la columna acepta 150 y
        // sin esto un mail largo pasa la validacion y revienta en Postgres como 500.
        RuleFor(x => x.Email)
            .MaximumLength(150)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("El cliente tiene que pertenecer a una zona.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
