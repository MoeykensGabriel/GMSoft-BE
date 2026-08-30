using FluentValidation;

namespace GMSoft.Application.Features.Customers.Update;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.BusinessName).MaximumLength(200);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        // El tope tiene que estar aunque el email sea opcional: la columna acepta 150 y
        // sin esto un mail largo pasa la validacion y revienta en Postgres como 500.
        RuleFor(x => x.Email)
            .MaximumLength(150)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.RouteOrder)
            .GreaterThan(0).When(x => x.RouteOrder is not null)
            .WithMessage("La posicion en el recorrido arranca en 1.");
    }
}
